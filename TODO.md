- Correctly merging with existing

  - Add a pass that removes all #if NET JUST_ATTRIBUTES #else/#endif blocks

- Handing child inherits parent

  - This means when processing a child we must be able to "look up" the parent class/struct and see what attributes it defines for each platform

- iOS implies Catalyst ?
