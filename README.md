I'd like to mostly integrate changes I make into the main Stride branch if they are broadly useful and polished, but some things aren't.
## Changes over stock:
- A BulletSharp fork with a much nicer kinematic character controller and an API giving you way more control
- Misc. quality of life things, some from Focus Engine
- Default gravity can be set in GameSettings
- Editor camera speed uses a min and a max value set in GUI, instead of an array of hardcoded steps
- Add a sort mode based on render group id #. Needs work if I'm going to bring it to main at some point