using BepInEx;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ShowDeathCause
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class ShowDeathCause : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "NotTsunami";
        public const string PluginName = "ShowDeathCause";
        public const string PluginVersion = "5.0.0";

        // The recap keeps the last few sources of damage from the final moments before a death
        private const int MaxRecapEntries = 5;
        private const float RecapWindowSeconds = 10f;

        internal struct DeathInfo
        {
            public bool IsOutOfBounds;
            public bool IsFallDamage;
            public bool IsVoidFog;
            public bool IsFriendlyFire;
            public bool IsVoidDeath;
            public bool Crit;
            public string DotToken;
            public string DamageTaken;
            // Only known on the host
            public string HealthBefore;
            public string Attacker;
            public BodyIndex AttackerBodyIndex;
            public bool FromClient;
        }

        internal class HitRecord
        {
            public string Attacker;
            public string DotToken;
            public float Damage;
            public int Hits;
            public bool Crit;
            public float Time;
        }

        // On the host this holds each player's killing blow; on clients it holds their latest hit,
        // which becomes the killing blow once they die
        internal static readonly Dictionary<CharacterMaster, DeathInfo> PlayerDeathInfos =
            [];

        internal static readonly Dictionary<CharacterMaster, List<HitRecord>> RecentHits =
            [];

        public static string GetAttacker(GameObject attacker, CharacterMaster attackerMaster)
        {
            if (attackerMaster && attackerMaster.playerCharacterMasterController)
                return attackerMaster.playerCharacterMasterController.networkUser.userName;

            if (!attacker) return Language.GetString("SDC_KILLER_ENVIRONMENT");

            var name = Util.GetBestBodyName(attacker);
            return name == "???" ? Language.GetString("SDC_KILLER_UNKNOWN") : name;
        }

        public static string GetAttacker(DamageReport damageReport)
        {
            return GetAttacker(damageReport.attacker, damageReport.attackerMaster);
        }

        public static bool IsVoidFogAttacker(DamageTypeCombo damageType, DamageColorIndex damageColorIndex,
            GameObject attacker, GameObject inflictor)
        {
            return damageColorIndex == DamageColorIndex.Void
                   && (damageType & DamageType.BypassArmor) != DamageType.Generic
                   && (damageType & DamageType.BypassBlock) != DamageType.Generic
                   && attacker == null
                   && inflictor == null;
        }

        public static bool IsVoidFogAttacker(DamageReport damageReport)
        {
            var damageInfo = damageReport.damageInfo;
            return IsVoidFogAttacker(damageInfo.damageType, damageInfo.damageColorIndex, damageInfo.attacker,
                damageInfo.inflictor);
        }

        internal static string GetDotToken(DotController.DotIndex dotIndex)
        {
            if (dotIndex == DotController.DotIndex.None) return null;

            // Modded DoTs sit past Count and have no token of their own
            return dotIndex < DotController.DotIndex.Count
                ? "SDC_DOT_" + dotIndex.ToString().ToUpperInvariant()
                : "SDC_DOT_GENERIC";
        }

        // Clients aren't sent the DoT type, but its damage color narrows it down
        internal static string GetDotToken(DamageTypeCombo damageType, DamageColorIndex damageColorIndex)
        {
            if ((damageType & DamageType.DoT) == DamageType.Generic) return null;

            return damageColorIndex switch
            {
                DamageColorIndex.Bleed => "SDC_DOT_BLEED",
                // Every burn variant uses the item color
                DamageColorIndex.Item => "SDC_DOT_BURN",
                DamageColorIndex.Poison => "SDC_DOT_POISON",
                DamageColorIndex.Void => "SDC_DOT_FRACTURE",
                DamageColorIndex.Electrocution => "SDC_DOT_ELECTROCUTION",
                _ => "SDC_DOT_GENERIC"
            };
        }

        internal static string FormatNumber(float value)
        {
            // The game sets CurrentCulture to match the selected language
            return value >= 100f
                ? value.ToString("N0", CultureInfo.CurrentCulture)
                : value.ToString("#,0.#", CultureInfo.CurrentCulture);
        }

        internal static string FormatAttacker(string attacker, string dotToken)
        {
            return dotToken == null
                ? attacker
                : Language.GetStringFormatted("SDC_ATTACKER_WITH_DOT", attacker, Language.GetString(dotToken));
        }

        internal static string FormatHit(HitRecord hit)
        {
            var text = FormatAttacker(hit.Attacker, hit.DotToken);
            if (hit.Hits > 1) text += " " + Language.GetStringFormatted("SDC_RECAP_HIT_COUNT", hit.Hits);
            text += ": " + FormatNumber(hit.Damage);
            if (hit.Crit) text += " " + Language.GetString("SDC_RECAP_CRIT");
            return text;
        }

        internal static DeathInfo CreateDeathInfo(DamageReport damageReport)
        {
            var damageInfo = damageReport.damageInfo;
            return new DeathInfo
            {
                IsOutOfBounds = (damageInfo.damageType & DamageType.OutOfBounds) != DamageType.Generic,
                IsFallDamage = damageReport.isFallDamage,
                IsVoidFog = IsVoidFogAttacker(damageReport),
                IsFriendlyFire = damageReport.isFriendlyFire,
                IsVoidDeath = (damageInfo.damageType & DamageType.VoidDeath) != DamageType.Generic,
                Crit = damageInfo.crit,
                DotToken = GetDotToken(damageReport.dotType),
                DamageTaken = FormatNumber(damageReport.damageDealt),
                HealthBefore = FormatNumber(damageReport.combinedHealthBeforeDamage),
                Attacker = GetAttacker(damageReport),
                AttackerBodyIndex = BodyCatalog.FindBodyIndex(damageInfo.attacker)
            };
        }

        // Mirrors CreateDeathInfo using only what vanilla sends to clients. The message has no inflictor,
        // so void fog is identified by its damage flags alone
        internal static DeathInfo CreateDeathInfo(DamageDealtMessage message, CharacterBody victimBody)
        {
            var attackerBody = message.attacker ? message.attacker.GetComponent<CharacterBody>() : null;
            var attackerTeam = attackerBody && attackerBody.teamComponent ? attackerBody.teamComponent.teamIndex : TeamIndex.None;
            var victimTeam = victimBody.teamComponent ? victimBody.teamComponent.teamIndex : TeamIndex.None;

            return new DeathInfo
            {
                IsOutOfBounds = (message.damageType & DamageType.OutOfBounds) != DamageType.Generic,
                IsFallDamage = (message.damageType & DamageType.FallDamage) != DamageType.Generic,
                IsVoidFog = IsVoidFogAttacker(message.damageType, message.damageColorIndex, message.attacker, null),
                // Same rules as DamageReport.isFriendlyFire
                IsFriendlyFire = victimBody.HasBuff(DLC4Content.Buffs.MalignantAttractionActive)
                                 || (attackerTeam == victimTeam && attackerTeam != TeamIndex.None),
                IsVoidDeath = (message.damageType & DamageType.VoidDeath) != DamageType.Generic,
                Crit = message.crit,
                DotToken = GetDotToken(message.damageType, message.damageColorIndex),
                DamageTaken = FormatNumber(message.damage),
                // Resolved now, since the attacker may be gone by the time the run ends
                Attacker = GetAttacker(message.attacker, attackerBody ? attackerBody.master : null),
                AttackerBodyIndex = BodyCatalog.FindBodyIndex(message.attacker),
                FromClient = true
            };
        }

        internal static void RecordHit(CharacterMaster victim, string attacker, string dotToken, float damage, bool crit)
        {
            if (!RecentHits.TryGetValue(victim, out var hits))
                RecentHits[victim] = hits = [];

            var now = Time.time;
            var last = hits.Count > 0 ? hits[^1] : null;

            // Fold repeated hits from the same source together so DoT ticks don't crowd out everything else
            if (last != null && last.Attacker == attacker && last.DotToken == dotToken)
            {
                last.Damage += damage;
                last.Hits++;
                last.Crit |= crit;
                last.Time = now;
            }
            else
            {
                hits.Add(new HitRecord
                {
                    Attacker = attacker,
                    DotToken = dotToken,
                    Damage = damage,
                    Hits = 1,
                    Crit = crit,
                    Time = now
                });
            }

            hits.RemoveAll(hit => now - hit.Time > RecapWindowSeconds);
            if (hits.Count > MaxRecapEntries) hits.RemoveRange(0, hits.Count - MaxRecapEntries);
        }

        private static void SetRecapTooltip(GameEndReportPanelController panel, CharacterMaster master,
            DeathInfo? deathInfo)
        {
            var image = panel.killerBodyPortraitImage;
            var tooltip = image.GetComponent<TooltipProvider>();

            // The panel is reused as the viewer flips between players, so always start from a clean slate
            if (!master || !RecentHits.TryGetValue(master, out var hits) || hits.Count == 0)
            {
                if (tooltip) tooltip.enabled = false;
                return;
            }

            if (!tooltip) tooltip = image.gameObject.AddComponent<TooltipProvider>();
            image.raycastTarget = true;

            var body = string.Join("\n", hits.Select(FormatHit));
            if (deathInfo?.HealthBefore != null)
                body += "\n\n" + Language.GetStringFormatted("SDC_RECAP_HEALTH_BEFORE", deathInfo.Value.HealthBefore);

            tooltip.titleColor = ColorCatalog.GetColor(ColorCatalog.ColorIndex.Blood);
            tooltip.bodyColor = Color.black;
            tooltip.overrideTitleText = Language.GetString("SDC_RECAP_TITLE");
            tooltip.overrideBodyText = body;
            tooltip.enabled = true;
        }

        // Vanilla icons are now Addressables-backed, so the direct sprite may be unset
        private static void SetArtifactIcon(RawImage image, ArtifactDef artifactDef)
        {
            var sprite = artifactDef.GetIcon();
            var iconReference = artifactDef.GetIconReference();
            if (!sprite && iconReference != null && iconReference.RuntimeKeyIsValid())
                sprite = Addressables.LoadAssetAsync<Sprite>(iconReference).WaitForCompletion();

            if (sprite) image.texture = sprite.texture;
        }

        private static Dictionary<string, List<KeyValuePair<string, string>>> LoadEmbeddedTokens()
        {
            const string prefix = "Language/";
            var tokensByLanguage = new Dictionary<string, List<KeyValuePair<string, string>>>(
                System.StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(ShowDeathCause).Assembly;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(prefix)) continue;
                var languageName = resourceName.Substring(prefix.Length).Split('/', '\\')[0];

                using var stream = assembly.GetManifestResourceStream(resourceName);
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
                var strings = SimpleJSON.JSON.Parse(reader.ReadToEnd())?["strings"];
                if (strings == null) continue;

                if (!tokensByLanguage.TryGetValue(languageName, out var tokens))
                    tokensByLanguage[languageName] = tokens = new List<KeyValuePair<string, string>>();
                foreach (var token in strings.Keys)
                    tokens.Add(new KeyValuePair<string, string>(token, strings[token].Value));
            }

            return tokensByLanguage;
        }

        private static void ApplyTokens(Dictionary<string, List<KeyValuePair<string, string>>> tokensByLanguage)
        {
            var english = Language.english ?? Language.FindLanguageByName("en");
            if (english != null && tokensByLanguage.TryGetValue("en", out var englishTokens))
                english.SetStringsByTokens(englishTokens);

            var current = Language.currentLanguage;
            if (current != null && current != english && tokensByLanguage.TryGetValue(current.name, out var tokens))
                current.SetStringsByTokens(tokens);
        }

        public void Awake()
        {
            Log.Init(Logger);

            var harmony = new Harmony(PluginGUID);
            new PatchClassProcessor(harmony, typeof(HarmonyPatches)).Patch();

            var tokensByLanguage = LoadEmbeddedTokens();
            Language.onCurrentLanguageChanged += () => ApplyTokens(tokensByLanguage);
            ApplyTokens(tokensByLanguage);

            Run.onRunStartGlobal += _ =>
            {
                PlayerDeathInfos.Clear();
                RecentHits.Clear();
            };

            // A new body means any earlier hit or death no longer applies
            CharacterBody.onBodyStartGlobal += body =>
            {
                if (!body.master) return;
                PlayerDeathInfos.Remove(body.master);
                RecentHits.Remove(body.master);
            };

            GlobalEventManager.onServerDamageDealt += damageReport =>
            {
                if (!damageReport.victimBody || !damageReport.victimBody.isPlayerControlled || !damageReport.victimMaster) return;

                RecordHit(damageReport.victimMaster, GetAttacker(damageReport), GetDotToken(damageReport.dotType),
                    damageReport.damageDealt, damageReport.damageInfo.crit);
            };

            // Clients never see the host's DamageReport, so track hits from the damage messages vanilla
            // already sends. This keeps the end game screen working for clients, even with a vanilla host
            GlobalEventManager.onClientDamageNotified += message =>
            {
                if (NetworkServer.active || !message.victim) return;

                var victimBody = message.victim.GetComponent<CharacterBody>();
                if (!victimBody || !victimBody.isPlayerControlled || !victimBody.master) return;

                var deathInfo = CreateDeathInfo(message, victimBody);
                PlayerDeathInfos[victimBody.master] = deathInfo;
                RecordHit(victimBody.master, deathInfo.Attacker, deathInfo.DotToken, message.damage, message.crit);
            };

            // onCharacterDeathGlobal now aliases onDeathProcServer, which is skipped for some deaths
            GlobalEventManager.onCharacterDeathServer += (damageReport) =>
            {
                if (damageReport == null) return;
                if (!damageReport.victimBody || !damageReport.victimBody.isPlayerControlled) return;

                var userName = Util.GetBestMasterName(damageReport.victimMaster);
                var deathInfo = CreateDeathInfo(damageReport);
                PlayerDeathInfos[damageReport.victimMaster] = deathInfo;

                string token;
                if (deathInfo.IsOutOfBounds)
                    token = "SDC_PLAYER_DEATH_OUT_OF_BOUNDS";
                else if (deathInfo.IsFallDamage)
                    token = "SDC_PLAYER_DEATH_FALL_DAMAGE";
                else if (deathInfo.IsVoidFog)
                    token = "SDC_PLAYER_DEATH_VOID_FOG";
                else if (deathInfo.IsFriendlyFire)
                    token = deathInfo.Crit ? "SDC_PLAYER_DEATH_FRIENDLY_CRIT" : "SDC_PLAYER_DEATH_FRIENDLY";
                else if (deathInfo.IsVoidDeath)
                    token = "SDC_PLAYER_DEATH_VOID";
                else
                    token = deathInfo.Crit ? "SDC_PLAYER_DEATH_CRIT" : "SDC_PLAYER_DEATH";

                // Format on the host so vanilla clients see readable text instead of unresolved tokens
                var attacker = FormatAttacker(deathInfo.Attacker, deathInfo.DotToken);
                var formattedMessage = Language.GetStringFormatted(token, userName, attacker, deathInfo.DamageTaken,
                    deathInfo.HealthBefore);
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = formattedMessage
                });

                // A single entry would only repeat the death message
                if (!RecentHits.TryGetValue(damageReport.victimMaster, out var hits) || hits.Count < 2) return;

                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = Language.GetStringFormatted("SDC_RECAP_CHAT", userName,
                        string.Join(", ", hits.Select(FormatHit)))
                });
            };
        }

        [HarmonyPatch]
        public class HarmonyPatches
        {
            [HarmonyPostfix, HarmonyPatch(typeof(GameEndReportPanelController),
                 nameof(GameEndReportPanelController.SetPlayerInfo))]
            public static void PatchEndGamePanel(
                GameEndReportPanelController __instance,
                RunReport.PlayerInfo __0)
            {
                var master = __0?.master;
                var isDead = master && __0.isDead;
                DeathInfo deathInfo = default;
                var hasDeathInfo = isDead && PlayerDeathInfos.TryGetValue(master, out deathInfo);

                SetRecapTooltip(__instance, isDead ? master : null, hasDeathInfo ? deathInfo : null);
                if (!isDead || !hasDeathInfo) return;

                // A client only sees hits, not the death itself. If vanilla disagrees about who landed the
                // killing blow (such as a scripted kill), keep the vanilla label rather than guess
                if (deathInfo.FromClient && deathInfo.AttackerBodyIndex != __0.killerBodyIndex) return;

                var attacker = FormatAttacker(deathInfo.Attacker, deathInfo.DotToken);
                var damageTaken = deathInfo.DamageTaken;

                string token;
                if (deathInfo.IsOutOfBounds)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                    attacker = Language.GetString("SDC_KILLER_OUT_OF_BOUNDS");
                }
                else if (deathInfo.IsFallDamage)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                    attacker = Language.GetString("SDC_KILLER_FALL_DAMAGE");
                    SetArtifactIcon(__instance.killerBodyPortraitImage, RoR2Content.Artifacts.weakAssKneesArtifactDef);
                }
                else if (deathInfo.IsVoidFog)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                    attacker = Language.GetString("SDC_KILLER_VOID_FOG");
                    __instance.killerBodyPortraitImage.texture = RoR2Content.Buffs.VoidFogMild.iconSprite.texture;
                }
                else if (deathInfo.IsFriendlyFire)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH_FRIENDLY";
                }
                else if (deathInfo.IsVoidDeath)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH_VOID";
                }
                else
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                }

                __instance.killerBodyLabel.text = Language.GetStringFormatted(token, attacker, damageTaken);
            }
        }
    }
}
