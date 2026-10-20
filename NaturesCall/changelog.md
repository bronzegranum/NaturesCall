- TODO Refactor code more nicely (as if)
- TODO 'TyronThreadPool.QueueTask(Action)' is obsolete: 'Use QueueTask with the caller parameter instead.'
- TODO Fix the bug where switching block or entity targets while peeing causes a random stream for a short moment (using new block hitposition on old block position?)
- TODO Stop urine and lant from washing away stains
- TODO Make right click urination have more particles!
- TODO Stop the hotkey peeing from being stopped by movement / being glitchy while moving

# v1.4.2

- **Fix**: Crash caused by failing to appropriately access server-side configs that I missed the last time I fixed this issue.

# v1.4.1

- **Added**: Support for xskillsfork
- **Fix**: Potential crash caused by client trying to access server's config incorrectly regarding urinating on entities

# v1.4.0

- **Added**: Client config setting that controls stream arcing (Literally: urine particle speed)
- **Added**: You can now urinate on entities. This upsets them.
- **Added**: Server config setting that controls if players can urinate on entities or not
- **Added**: Lant now works to soak salted hides
- **Added**: You can now urinate into placed jugs, bowls and other open top containers
- **Fix**: Urinating with the hotkey and using right click now produces nearly identical results, though rightclicking still has less particles
- **Fix**: Crash when looking at urine in handbook with HoD installed (HoD distilled water name changed)
- **Fix**: Crash when urinating while looking at an entity
- **Fix**: Bladder Fill Multiplier setting not saving in the config
- **Tweak**: Updated from net 8.0 to net 10.0
- **Tweak**: Changed from CakeBuild to ZZCakeBuild (Vintage Story specific version of CakeBuild)
- **Tweak**: Altered code to no longer force latest lang version (known to cause instability)
- **Tweak**: Lant now uses the vanilla tagging system for hides it can soak
- **Tweak**: Urine particles now spawn from half of the character's eye level (roughly the hips)
- **Tweak**: Altered particle velocity calculations for better accuracy and added a variable for particle speed

# v1.3.6

- **Fix**: crash when looking at urine in handbook with HoD installed
- **Tweak**: change modicon: screenshot from `Blinky Dorf` on discord

