# Public source preparation

License: MIT, chosen by the author. Applies to this project's own code, documentation, generated translations and original generated artwork; game assemblies and external toolchains are not included or relicensed.

The source tree contains readable C# sources, host API doubles and regression checks, final resource textures, artwork sources and prompts, eight locale CSVs, and portable build instructions. English and Simplified Chinese Workshop descriptions are separate Steam BBCode files. The square Workshop cover includes a full PNG and a smaller JPG export.

Before the public push, confirm that the tracked tree and complete Git history contain no provider credentials, player saves, game/third-party DLLs, Remis application state or private test-money Mod. Generated local receipts remain ignored under `outputs/`. The test-money repository must retain its private/local scope.

The Mod does not perform networking or load arbitrary files. Its only Harmony patch is scoped to this Mod's suppliers and guards wholesale invocation. It adds native services, custom item factories and customer hooks. Source availability allows inspection; it is not a guarantee about every game build or Mod combination.

Public repository: https://github.com/Drlinglong/ProbablyStolenScavengerTeams. It contains only this independent project. Both Workshop descriptions include this verified repository URL. A prepared local release, Git tag, or description does not establish that GitHub or Steam publication has occurred.

Do not upload a Remis companion package: all eight language CSVs are already embedded in ScavengerTeams. The runtime release should contain only `ScavengerTeams.dll` as its assembly.
