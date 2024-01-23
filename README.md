# YAFC: Community Edition
### Why new repo?
The [original](https://github.com/ShadowTheAge/yafc) YAFC repository has been inactive for years. Bugfixes piled up, but there was no one to review and merge them. This repository aims to solve that problem.

### Have you talked with the author?
Yes, we have their approval.
<details>
<summary>Expand to see the screenshot</summary>
<IMG src="/Docs/Media/yafc_author_approval.png"  alt="yafc_author_approval.png"/>
</details>

## What is YAFC?
Yet Another Factorio Calculator or YAFC is a planner and analyser. Its main goal is to help with heavily modded Factorio games.

<details>
<summary>Expand to have a quick glance at what YAFC can do</summary>
<IMG src="/Docs/Media/Main.gif"  alt="Main.gif"/>
</details>

YAFC is more than just a calculator. It uses multiple algorithms to understand what is going on in your modpack, to the point of calculating the whole late-game base. It knows what items are more important, and what recipes are more efficient.

It was created as an answer to deeply recursive Pyanodon recipes, which the tools like Helmod could not handle. YAFC uses Google's [OrTools](https://developers.google.com/optimization) as a model solver to handle them extremely well.

Among other things YAFC has Never Enough Items, which is FNEI on steroids. In addition to showing the recipes, it shows which ones you probably want to use, and how much.

## Project features
- Works with any combination of mods for Factorio 0.17+.
- Multiple pages, the Undo button (Ctrl+Z).
- Dependency Explorer tool that allows to see which objects are needed for what.
- Never Enough Items tool that helps to find out how to produce any item, and also which way YAFC thinks is optimal.
- Main calculator sheet:
    - Links: YAFC will try to balance production/consumption only for linked goods. Unlinked goods are calculated but not balanced. It is a core difference from Helmod, which attempts to balance everything and breaks on deeply recursive recipes.
    - Nested sheets: You can attach a nested sheet to any recipe. When a sheet is collapsed, you will see a summary for all recipes in it. Nested sheets have their own set of links. For example, you can create a nested sheet for electronic circuits, and put copper cables inside that sheet. If you add an internal link for copper cables, it will be separate, so you can calculate copper cables just for electronic circuits.
    - Auto modules: You can add modules to recipes by using a single slider. Based on your milestones, it will automatically add modules you have access to. It will prioritize putting modules into buildings that benefit the most from them. <details><summary>Expand to see it in action</summary><IMG src="/Docs/Media/AutoModules.gif"  alt="AutoModules.gif"/></details>
    - Fluid temperatures, although without mixing them, allow to calculate energy generation.
    - Fuel, including electricity. You can even add energy generation exactly enough for your sheet. Howerver, inserters are not included.
- Multiple analyses:
    - Accessibility analysis: Shows inaccessible objects. Mods often hide objects, and Factorio has a bunch of hidden ones too. However, it is impossible to find objects that are spawned by mods or map scripts. This analysis may fail for modpacks like Seablock, but you can mark some objects as accessible manually.
    - Milestone analysis: You can add anything as a milestone. YAFC will display that milestone icon on every object that is locked behind it, directly or indirectly. Science packs are natural milestones, and so they are added by default.
    - Automation analysis: YAFC tries to find objects that can be fully automated. For example, wood in a vanilla game cannot be fully automated because it requires to cut trees.
    - Cost analysis: YAFC assigns a cost to each object. The cost is a sum of logistic actions you need to perform to get that object, using the most optimal recipes. YAFC cost is very useful to quickly compare items and recipes. This cost also helps to find which recipes are suboptimal.
    - Flow analysis: YAFC calculates a base that produces enough science packs for all non-infinite research. It knows how much of everything you will probably need.

More gifs can be found [here](/Docs/Gifs.md) (Traffic warning!)

## Possible incompatibilities

YAFC loads mods in an environment that is not completely compatible with Factorio. If you notice any bugs, please report them in the [issues](https://github.com/have-fun-was-taken/yafc-ce/issues).

> I am playing Seablock / Other "scripted progression" mod, and YAFC thinks that items are inaccessible

No ultimate solution to this has been found, but you can open Dependency Explorer and manually mark a bunch of items or technologies as accessible.

For Seablock specifically, please check [this](https://github.com/ShadowTheAge/yafc/issues/31) issue that contains a small list of things to enable at first.

For mod authors: You can detect YAFC by checking the `data.data_crawler` variable during the data stage. It will be equal to `yafc a.b.c.d` where `a.b.c.d` is yafc version. For instance, `yafc 0.5.4.0`.
	
## **[Download YAFC](https://github.com/ShadowTheAge/yafc/releases)**

YAFC is a desktop app. Windows build is the most tested, but OSX and Linux are there too. See [Linux and OSX installation instructions](/Docs/LinuxOsxInstall.md).

## License
- [GNU GPL 3.0](/LICENSE)
- Copyright 2020 © ShadowTheAge
- This readme contains gifs featuring Factorio icons. All Factorio icons are copyright of Wube Software.
- Powered by free software: .NET core, SDL2, Google Or-Tools, Lua and others (see [full list](/licenses.txt)).
