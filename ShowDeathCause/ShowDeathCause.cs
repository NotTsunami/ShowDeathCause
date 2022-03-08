using BepInEx;
using HarmonyLib;
using RoR2;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global
// ReSharper disable once InconsistentNaming
// ReSharper disable StringLiteralTypo
namespace ShowDeathCause
{
    [BepInPlugin("dev.tsunami.ShowDeathCause", "ShowDeathCause", "3.0.1")]
    public class ShowDeathCause : BaseUnityPlugin
    {
        // These members are added to avoid trying to later access a GameObject that doesn't exist
        private static DamageReport _damageReport;
        private static string _damageTaken;
        private static string _attacker;

        public static string GetAttacker(DamageReport damageReport)
        {
            // Standard code path
            if (damageReport.attackerMaster)
            {
                return damageReport.attackerMaster.playerCharacterMasterController ? damageReport.attackerMaster.playerCharacterMasterController.networkUser
                    .userName : Util.GetBestBodyName(damageReport.attackerBody.gameObject);
            }

            // For overrides like Suicide() of type VoidDeath, return damageReport.attacker, otherwise ???
            return damageReport.attacker ? Util.GetBestBodyName(damageReport.attacker) : "???";
        }

        public static bool IsVoidFogAttacker(DamageReport damageReport)
        {
            var damageInfo = damageReport.damageInfo;

            // Checking done by referencing FogDamageController's EvaluateTeam()
            return damageInfo.damageColorIndex == DamageColorIndex.Void
                   && damageInfo.damageType.HasFlag(DamageType.BypassArmor)
                   && damageInfo.damageType.HasFlag(DamageType.BypassBlock)
                   && damageInfo.attacker == null
                   && damageInfo.inflictor == null;
        }

        public void Awake()
        {
            // We use a Harmony patch to avoid generating and bundling our own MMHook
            var harmony = new Harmony(Info.Metadata.GUID);
            new PatchClassProcessor(harmony, typeof(HarmonyPatches)).Patch();

            // For some reason, we were unable to get languages to properly add via Zio, thus they are
            // added directly via hooking onto the onCurrentLanguageChanged event. This isn't awful,
            // but it does add some extra code that can be avoided. Pull requests that shift this to
            // Zio and local files are welcome.
            Language.onCurrentLanguageChanged += () =>
            {
                var list = new List<KeyValuePair<string, string>>();
                if (Language.currentLanguage.TokenIsRegistered("SDC_KILLER_FALL_DAMAGE")) return;
                if (Language.currentLanguageName == "en")
                {
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_FALL_DAMAGE", "<color=#964B00>Fall Damage</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_VOID_FOG", "<color=#753f8a>Void Fog</color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH", "<color=#FFFFFF>Killed By:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} damage)</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_FRIENDLY", "<color=#FFFFFF>Killed By:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} damage) <color=#32a852>(FF)</color></color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_VOID", "<color=#FFFFFF>Killed By:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} damage) <color=#FF8000>(Jail)</color></color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID_FOG", "<color=#00FF80>{0}</color> died to <color=#753f8a>void fog</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FALL_DAMAGE", "<color=#00FF80>{0}</color> died to <color=#964B00>fall damage</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY", "<color=#32a852>FRIENDLY FIRE!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY_CRIT", "<color=#32a852>FRIENDLY FIRE!</color> <color=#FF0000>CRITICAL HIT!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH", "<color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_CRIT", "<color=#FF0000>CRITICAL HIT!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID", "<color=#621e7d>JAILED!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color>."));
                }
                else if (Language.currentLanguageName == "ko")
                {
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_FALL_DAMAGE", "<color=#964B00>추락 대미지</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_VOID_FOG", "<color=#753f8a>공허 안개</color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH", "<color=#FFFFFF>다음에게 사망:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} 대미지)</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_FRIENDLY", "<color=#FFFFFF>다음에게 사망:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} 대미지) <color=#32a852>(팀킬)</color></color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_VOID", "<color=#FFFFFF>다음에게 사망:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} 대미지) <color=#FF8000>(공허 감옥)</color></color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID_FOG", "<color=#00FF80>{0}</color> 님이 <color=#753f8a>공허 안개</color>에 의해 죽었습니다 ({2} 대미지를 받음)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FALL_DAMAGE", "<color=#00FF80>{0}</color> 님이 <color=#964B00>추락 대미지</color>로 인해 죽었습니다 ({2} 대미지를 받음)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY", "<color=#32a852>팀킬!</color> <color=#00FF80>{0}</color> 님이 <color=#FF8000>{1}</color> 님에게 죽었습니다 ({2} 대미지를 받음)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY_CRIT", "<color=#32a852>팀킬!</color> <color=#FF0000>치명타!</color> <color=#00FF80>{0}</color> 님이 <color=#FF8000>{1}</color> 님에게 죽었습니다 ({2} 대미지를 받음)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH", "<color=#00FF80>{0}</color> 님이 <color=#FF8000>{1}</color>에게 죽었습니다 ({2} 대미지를 받음)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_CRIT", "<color=#FF0000>치명타!</color> <color=#00FF80>{0}</color>에게 죽었습니다 <color=#FF8000>{1}</color> ({2} 대미지를 받음)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID", "<color=#621e7d>공허 감옥에 갖힘!</color> <color=#00FF80>{0}</color>에게 죽었습니다 <color=#FF8000>{1}</color>."));
                }
                else if (Language.currentLanguageName == "zh-CN")
                {
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_FALL_DAMAGE", "<color=#964B00>坠落伤害</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_VOID_FOG", "<color=#753f8a>虚空之雾</color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH", "<color=#FFFFFF>被</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>杀死 ({1} 伤害)</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_FRIENDLY", "<color=#FFFFFF>被</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>杀死 ({1} 伤害) <color=#32a852>(友伤)</color></color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_VOID", "<color=#FFFFFF>被</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>杀死 ({1} 伤害) <color=#FF8000>(空狱)</color></color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID_FOG", "<color=#00FF80>{0}</color>死于 <color=#753f8a>虚空之雾</color> (承受了 {2} 伤害)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FALL_DAMAGE", "<color=#00FF80>{0}</color>死于 <color=#964B00>坠落伤害</color> (承受了 {2} 伤害)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY", "<color=#32a852>中出叛徒!</color> <color=#00FF80>{0}</color> 被 <color=#FF8000>{1}</color> 杀死 (承受了 {2} 伤害)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY_CRIT", "<color=#32a852>中出叛徒!</color> <color=#FF0000>暴击!</color> <color=#00FF80>{0}</color> 被 <color=#FF8000>{1}</color> 杀死 (承受了 {2} 伤害)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH", "<color=#00FF80>{0}</color> 被 <color=#FF8000>{1}</color> 杀死 (承受了 {2} 伤害)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_CRIT", "<color=#FF0000>暴击!</color> <color=#00FF80>{0}</color> 被 <color=#FF8000>{1}</color> 杀死 (承受了 {2} 伤害)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID", "<color=#621e7d>坐牢!</color> <color=#00FF80>{0}</color> 被 <color=#FF8000>{1}</color> 杀死."));
                }
                else
                {
                    // Fallback to English for unsupported languages
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_FALL_DAMAGE", "<color=#964B00>Fall Damage</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_KILLER_VOID_FOG", "<color=#753f8a>Void Fog</color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH", "<color=#FFFFFF>Killed By:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} damage)</color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_FRIENDLY", "<color=#FFFFFF>Killed By:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} damage) <color=#32a852>(FF)</color></color>"));
                    list.Add(new KeyValuePair<string, string>("SDC_GENERIC_PREFIX_DEATH_VOID", "<color=#FFFFFF>Killed By:</color> <color=#FFFF80>{0}</color> <color=#FFFFFF>({1} damage) <color=#FF8000>(Jail)</color></color>"));

                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID_FOG", "<color=#00FF80>{0}</color> died to <color=#753f8a>void fog</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FALL_DAMAGE", "<color=#00FF80>{0}</color> died to <color=#964B00>fall damage</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY", "<color=#32a852>FRIENDLY FIRE!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_FRIENDLY_CRIT", "<color=#32a852>FRIENDLY FIRE!</color> <color=#FF0000>CRITICAL HIT!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH", "<color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_CRIT", "<color=#FF0000>CRITICAL HIT!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken)."));
                    list.Add(new KeyValuePair<string, string>("SDC_PLAYER_DEATH_VOID", "<color=#621e7d>JAILED!</color> <color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color>."));
                }
                Language.currentLanguage.SetStringsByTokens(list);
            };

            // Subscribe to the pre-existing event, we were being a bad boy and hooking onto the GlobalEventManager before
            GlobalEventManager.onCharacterDeathGlobal += (damageReport) =>
            {
                // This should never happen, but protect against it just in case
                if (damageReport == null) return;

                // Don't activate for non-player entities
                if (!damageReport.victimBody.isPlayerControlled || !damageReport.victimBody) return;

                // Util.GetBestMasterName gets the userName while checking for null
                var userName = Util.GetBestMasterName(damageReport.victimMaster);

                _damageReport = damageReport;
                _damageTaken = $"{damageReport.damageInfo.damage:F2}";
                _attacker = GetAttacker(damageReport);

                string token;
                if (damageReport.isFallDamage)
                {
                    token = "SDC_PLAYER_DEATH_FALL_DAMAGE";
                } 
                else if (IsVoidFogAttacker(damageReport))
                {
                    token = "SDC_PLAYER_DEATH_VOID_FOG";
                }
                else if (damageReport.isFriendlyFire)
                {
                    token = damageReport.damageInfo.crit ? "SDC_PLAYER_DEATH_FRIENDLY_CRIT" : "SDC_PLAYER_DEATH_FRIENDLY";
                }
                else if ((damageReport.damageInfo.damageType & DamageType.VoidDeath) != DamageType.Generic)
                {
                    token = "SDC_PLAYER_DEATH_VOID";
                }
                else
                {
                    token = damageReport.damageInfo.crit ? "SDC_PLAYER_DEATH_CRIT" : "SDC_PLAYER_DEATH";
                }

                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = token,
                    paramTokens = new[] { userName, _attacker, _damageTaken }
                });
            };
        }

        [HarmonyPatch]
        public class HarmonyPatches
        {
            [HarmonyPostfix, HarmonyPatch(typeof(RoR2.UI.GameEndReportPanelController),
                 nameof(RoR2.UI.GameEndReportPanelController.SetPlayerInfo))]
            public static void PatchEndGamePanel(RoR2.UI.GameEndReportPanelController __instance)
            {
                // This should never happen, but leave original text in the case the report is null
                if (_damageReport == null) return;
    
                // Override the string for killerBodyLabel ("Killed By: <killer>" on the end game report panel)
                string token;
                if (_damageReport.isFallDamage)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                    _attacker = Language.GetString("SDC_KILLER_FALL_DAMAGE");
                    __instance.killerBodyPortraitImage.texture = RoR2Content.Artifacts.weakAssKneesArtifactDef.smallIconSelectedSprite.texture;
                }
                else if (IsVoidFogAttacker(_damageReport))
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                    _attacker = Language.GetString("SDC_KILLER_VOID_FOG");
                    __instance.killerBodyPortraitImage.texture = RoR2Content.Buffs.VoidFogMild.iconSprite.texture;
                }
                else if (_damageReport.isFriendlyFire)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH_FRIENDLY";
                }
                else if ((_damageReport.damageInfo.damageType & DamageType.VoidDeath) != DamageType.Generic)
                {
                    token = "SDC_GENERIC_PREFIX_DEATH_VOID";
                }
                else
                {
                    token = "SDC_GENERIC_PREFIX_DEATH";
                }
                __instance.killerBodyLabel.text = Language.GetStringFormatted(token, _attacker, _damageTaken);
            }
        }
    }
}
