# Third-party components

## KamiToolKit

Native FFXIV Atk UI toolkit by MidoriKami. MIT License, copyright 2024 MidoriKami.

Source: https://github.com/MidoriKami/KamiToolKit
Pinned commit: b4b7cab18e3b60abb49391272acb421e35b52bad

The vendored source is under `vendor/KamiToolKit`, including its LICENSE. The installable ZIP contains KamiToolKit.dll, its runtime assets and KamiToolKit-LICENSE.txt. Hunt Route constructs only its own addon, HuntRouteConfirm; it does not modify shared game confirmation callbacks.

## HuntHelper data

Earlier zone-wide B-rank positions (through 0.3.3) were derived from img02/HuntHelper, commit 0c07d87a1e951f9dce7a17e500d41bbc752bee94 (MIT, copyright 2022 imaginary-png). HuntHelper-LICENSE.txt is included in the installable ZIP.

## HuntBuddy reference

Daily location facts were extracted from SheepGoMeh/HuntBuddy, commit 1de61e25b99b6473f4d2c75b4023e588016f0aa4. HuntBuddy was also inspected for the use of the game's hunt portraits.

## Target-specific B-rank coordinates

Since 0.3.4, `data/elite-spawns.json` maps each BNpcName ID to its own territory/map/point set. Source: RKI027/ffxiv-huntmaps-maker, commit 047ac3fddf28eb3232c59ed7006d085584963a9d, `data/marks.json` (MIT, copyright 2019 Arkhelyi). Huntmaps-LICENSE.txt is included in the installable ZIP. All 77 names and zones were bound to installed game data; case normalization handles the displayed name of ü-u-ü-u.

## Daily coordinate review references

The 476 existing HuntBuddy daily anchors were compared to the current public datasets on https://www.xivdaily.com/hunts/ and the individual observation maps linked from https://www.ffxivhunt.com/hunt_daily_hw.php (and the sb/shb/end/dt pages), checked 2026-11-07. Reference image files are not redistributed. `data/coordinate-audit.json` stores factual positions, reference URLs and review outcomes. Different valid groups of the same mob were not treated as errors or bulk-replaced. See COORDINATE-AUDIT.md for limitations.
