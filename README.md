# Mellite - A mass metadata conversion tool

Mellite is a tool for making large scale changes of metadata on C# source code bases.

## Name

[Meta](<https://en.wikipedia.org/wiki/Meta_(mythology)>) was the daughter of Hoples who became the first wife of Aegeus, king of Athens. Some traditions however name her as Mellite.

It seemed fitting that a tool for mass renaming "Meta"-data is an alternate name for Meta.

## Assembly Harvest Setup

Some of the options, add-default-introduced and harvest-assembly require Xamarin Platform assemblies to "harvest". Since code behind files don't have access to attributes declared in generated files, it is not possible to "know"
the parent attributes to copy down. Assembly Harvesting is how this is resolved, by using Cecil to process those Assemblies to gather the attributes.

To correctly get _all_ attributes, a special build of the legacy platform assemblies is needed, with [this patch](https://gist.github.com/chamons/7cb21ad92777b9dfd223eb65b8f87cc0) applied.
