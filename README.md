# ShowDeathCause
This mod lets you know how your and your friends miserable lives ended. This is based on the original [ShowDeathCause](https://github.com/MOV-MB/ShowDeathCause) with several improvements and updated for the latest game version.

## Features
The death notice lets everyone know what killed you and how. The end game report screen is improved too! This fork provides the following improvements over the old version:
- Works with the latest game version (Hallowed Concepts)
- Handles several more scenarios (friendly fire, critical hits, fall damage, void fog, void deaths, out of bounds)
- Prints the name of the dead player along with damage taken and the health they had before the killing blow
- Names the damage over time effect that finished you off (bleed, burn, poison and more)
- **(NEW)** Death recap: hover the killer portrait on the end game report screen to see the last hits you took, and the host posts them to chat
- Updates the end game report screen to include the enhanced information, for every player running the mod (not just the host)
- Vanilla compatible: no R2API, and players without the mod still see the chat messages
- Translated into English, Korean and Simplified Chinese (pull requests are always welcome! See the guide below for contributing.)

| Chat Message (Before) | Chat Message (After) |
| ----- | ----- |
| ![Before](https://raw.githubusercontent.com/NotTsunami/ShowDeathCause/master/Images/ExampleChatBefore.jpg) | ![After](https://raw.githubusercontent.com/NotTsunami/ShowDeathCause/master/Images/ExampleChatAfter.jpg) |

| Game End Report (Before) | Game End Report (After) |
| ------ | ------ |
| ![Before](https://raw.githubusercontent.com/NotTsunami/ShowDeathCause/master/Images/ExampleBefore.jpg) | ![After](https://raw.githubusercontent.com/NotTsunami/ShowDeathCause/master/Images/ExampleAfter.jpg) |

_* Screenshots are taken on ShowDeathCause 2.0.0_

## Installation
It is highly recommended to use [r2modman](https://thunderstore.io/package/ebkr/r2modman/) to install ShowDeathCause because it will set up everything for you! If you are installing manually, you will need to make a folder in `Risk of Rain 2\BepInEx\plugins` called `ShowDeathCause` and drop the contents of the zip into it.

## Changelog
### Version 5.0.0
- Updated for the Alloyed Collective and Hallowed Concepts updates
- **Death recap**: the last hits a player took before dying are shown in a tooltip on the end game report's killer portrait, and the host posts them to chat. Repeated hits from the same source (such as DoT ticks) are grouped together
- Death messages name the damage over time effect behind the killing blow and the health the player had before it
- The end game report tracks each player's death separately, and clients running the mod get it too, even when the host doesn't have the mod
- Death messages are formatted by the host, so players without the mod see readable text
- Damage shown is now the damage actually dealt after armor
- New labels for out of bounds deaths and attackers that previously showed as `???`
- Damage numbers use the game language's number format, and drop decimals on large numbers
- Translations moved out of the code into the game's own language file format and embedded in the DLL, with no R2API needed
- Plugin GUID changed to `NotTsunami.ShowDeathCause`
- Build now produces a Thunderstore-ready zip

### Version 3.0.1
- Use builtin functions when applicable
- Ensure victim body is valid
- Only add language tokens if they aren't already added

### Version 3.0.0
Version 3.0.0 is a MAJOR release, with a lot of changes under the hood. A very large shoutout goes to [DestroyedClone](https://github.com/DestroyedClone) for the support of additional languages along with the newly added death messages, and [Bubbet](https://github.com/Bubbet) for helping me learn Harmony. I recommend showing your support for these two by checking out their mods!
- R2API dependency has been removed, making ShowDeathCause vanilla-compatible!
- Language support has been added
- New death messages have been added for the DLC update (void fog and void instant kill abilities)
- Under the hood changes, mostly some future-proofing and hooking correctly

### Version 2.0.2
- Fixed NullReferenceException when the killer's body no longer exists in between the death message and end game screen
    - This happened most commonly when being killed by a Jellyfish

### Version 2.0.1
- Reverted change to remove original death message as this may break other mods that depend on `OnPlayerCharacterDeath`

### Version 2.0.0 - Happy almost 100k downloads!
- The biggest change in this update is that the end game report screen now includes the same information as the death notice
- Elite/Umbra prefixes are now shown (Thanks WolfgangIsBestWolf for the suggestion!)
- Fall damage is now labeled as such (Can occur when max HP <= 1 or with artifact)
- Friendly fire kills are now attributed to the display name of the player who killed them
- The original red death message is no longer shown, only the messsage from ShowDeathCause is printed in chat now

### Version 1.0.5
- Switched to local stripped libs instead of relying on game's installation
- Updated for anniversary update

### Version 1.0.4
- Updated for Risk of Rain 2 version 1.0

### Version 1.0.3
- Catch case where there are no attackers

### Version 1.0.2
- Artifacts update!
- Removed enemy skill and capped float value to 2 decimal points

## Contributing a Translation

Thank you for your interest in contributing a translation! You can contribute a translation by following the steps below:

1. Translations live in `ShowDeathCause/Language/<language>/ShowDeathCause.json`, where `<language>` is the name of the game's own language folder (found in `Risk of Rain 2_Data/StreamingAssets/Language`, e.g. `FR` or `de`).
2. Copy `ShowDeathCause/Language/en/ShowDeathCause.json` into a new folder for your language, for example `ShowDeathCause/Language/FR/ShowDeathCause.json`.
3. Translate each value in the `strings` object, leaving the token names on the left untouched. Any token you leave out falls back to English.
   The files are embedded into `ShowDeathCause.dll` when it's built, so rebuild to test your changes in game.
4. Send in a pull request through GitHub!

Anything in brackets ({}) or wrapped in <> does not need to be translated. For example, if you were translating the `SDC_PLAYER_DEATH` token, which reads `<color=#00FF80>{0}</color> killed by <color=#FF8000>{1}</color> ({2} damage taken at {3} health).` as of version 5.0.0, you would only translate "killed by", "damage taken at" and "health".

## Credits
### Original Authors
- [MOV-MB](https://github.com/MOV-MB)
- [MagnusMagnuson](https://thunderstore.io/package/MagnusMagnuson/)

### Other Credits
As noted in the changelog, the 3.0.0 release would not have been possible without the help of the following:
- [DestroyedClone](https://github.com/DestroyedClone) (https://github.com/NotTsunami/ShowDeathCause/pull/4) 
- [Bubbet](https://github.com/Bubbet)
- Everyone in the Risk of Rain 2 Modding Discord that has helped and supported me!
- [Skull icon](https://icons8.com/icons/set/skull) by [Icons8](https://icons8.com)
