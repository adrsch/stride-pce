This fork will get my custom scripting language SeqScript in it at some point. Right now it's game-side, not engine side, except some small bits. Or I could make it a separate extension. Not sure yet.

Scenes and entities have commands attached to them.
(I could make this an attribute for ease of use...)

```
x.Commands["foo"] = new CommandInfo
{
    Params = new List<Type>(typeof(string)), // args[0]
    OptionalParams = new List<Type>(typeof(string)), // args[1] if present, otherwise args[1] will be the default for whatever type
    Exec = async args => await bar((string)args[0], (string)args[1]),
    Help = "Help for the command",
}
```

Scenes must be specified except for the root scene, which is implicit.

```
scene:command args;
scene:entity:childentity:command args;
commandOnRootScene args;
entityInRootScene:command args;
```

## Other changes:
- A BulletSharp fork with a much nicer kinematic character controller and an API giving you way more control
- Misc. quality of life things, some from Focus Engine
- ~~Default gravity can be set in GameSettings~~
- ~~FBX importer bug fix~~
- Editor camera speed uses a min and a max value set in GUI, instead of an array of hardcoded steps*. ~~User can set speed up/down keybinds in editor settings~~
- Add a sort mode based on render group id #. Needs work if I'm going to bring it to main at some point
- Audio volume is split into buses. Audio emitter component handles sound controllers automatically. Built in pitch variance.
- ~~FBX importer lets you select which animation stack is imported~~

*has actual conflicts with changes in the stride repo since I forked

I'd like to mostly integrate changes I make into the main Stride branch if they are broadly useful and polished, but some things aren't.