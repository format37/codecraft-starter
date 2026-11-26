# CodeCraft starter

A ready-to-submit strategy for **CodeCraft**, the bot-programming game of 2020, in every language the
[CodeCraft](https://codecraft.day) server accepts. Clone it, pick your language, make it yours.

Each folder is the official 2020 client for that language (the game model, the protocol, the runner)
with the same starter strategy dropped in. The strategy is deliberately small (about seventy lines of Python)
and does the same thing everywhere:

- builders step to the right, mining the resources in their way;
- with enough resources and no house yet, they put up one house just right of the builder base, then repair it
  until it is finished;
- in the local runner it draws the planned house on the map and logs how many entities of each type you have
  and the house's health; on the server, where there is no viewer, it skips the drawing and the logs.

That is all: the bases train nobody and nothing fights. The server's built-in QuickStart bot plays this same
strategy, so a game against it is a mirror match until you change yours. The rest of the game (training units,
gathering, building, fighting) is yours to add.

| Language on the submit page | Folder | Strategy file |
|---|---|---|
| C++ | `cpp/` | `MyStrategy.cpp` |
| C# | `csharp/` | `MyStrategy.cs` |
| D | `dlang/` | `source/my_strategy.d` |
| F# | `fsharp/` | `MyStrategy.fs` |
| Go | `go/` | `my_strategy.go` |
| Java, Java (GraalVM native) | `java/`, `java-graalvm-native/` | `src/main/java/MyStrategy.java` |
| JavaScript | `javascript/` | `my-strategy.js` |
| Kotlin, Kotlin (GraalVM native) | `kotlin/`, `kotlin-graalvm-native/` | `src/main/kotlin/MyStrategy.kt` |
| Python, Python (PyPy) | `python/` | `my_strategy.py` |
| Ruby | `ruby/` | `my_strategy.rb` |
| Rust | `rust/` | `src/my_strategy.rs` |
| Scala | `scala/` | `src/main/scala/MyStrategy.scala` |

## Submit it

On [codecraft.day/submit](https://codecraft.day/submit) you name your strategy,
**choose the language** and give the source in one of three ways. The language you choose is what decides
which folder is built.

1. **Git URL.** Paste `https://github.com/format37/codecraft-starter` (better: your fork of it) and pick a
   language. A repository with one folder per language is built from the folder named after the language
   (`python-pypy` falls back to `python/`). A branch, tag or commit can be given next to the URL.
2. **Zip.** Zip one language folder, or the whole repository, and upload it.
3. **One file.** Upload just the strategy file (`my_strategy.py`, `MyStrategy.java`, ...); the server puts it
   into the official client of the chosen language.

Submitting again under the same strategy name makes a new version; the latest successful build plays. Each new
version plays its rated games by itself; the [leaderboard](https://codecraft.day/leaderboard) shows where it
stands. A coding agent (Claude Code, Codex, Cursor, any MCP client) can submit, play and read the games for you
with a token from [codecraft.day/agents](https://codecraft.day/agents).

## Make it yours

Fork, change the strategy file of your language, push, submit the fork's URL. The rules of the game and the
whole entity model are on [codecraft.day/rules](https://codecraft.day/rules), and in the
[server repository](https://github.com/format37/codecraft) as `docs/doc-en.md`.

To test on your machine, use the local runner: the server's engine with the game live in the browser and the debug
interface your strategy draws and logs through. The guide with the commands of every language is
[codecraft.day/quickstart](https://codecraft.day/quickstart); in short, in this folder:

```bash
docker run --rm -it -p 31000:31000 -p 31001-31004:31001-31004 -v "$PWD":/game ghcr.io/format37/codecraft-runner
# or, without Docker (also linux-arm64 and macos):
curl -L https://github.com/format37/codecraft/releases/latest/download/codecraft-runner-linux-x86_64.tar.gz | tar xz && ./codecraft-runner
```

On Windows 10 or 11, in PowerShell or cmd (the exe is not signed: SmartScreen may ask once, More info, then Run
anyway):

```powershell
curl.exe -L -o runner.zip https://github.com/format37/codecraft/releases/latest/download/codecraft-runner-windows-x86_64.zip
tar xf runner.zip
.\codecraft-runner.exe
```

Then start your strategy: each client's runner takes `host port` (and an optional token) and connects to
`127.0.0.1 31001` by default; in Python that is `python3 main.py` (`python main.py` on Windows). The runner marks
the ticks the server would not allow (more than 1 s to answer a tick, or 40 s in all) but waits for your strategy,
so a breakpoint does no harm; `--limits server` (or the viewer's side panel) cuts it off as the server does.

Only the strategy file is yours to edit: the rest of each folder is the client, and the server builds every
folder exactly like the 2020 contest did (`compile.sh` of the official client, then `run.sh`).

## Keep it fast

The server gives a strategy 1 s to answer each tick and 40 s of CPU time for the whole game, and cuts it off past
either. A strategy often slows down as the game goes on, so one that is quick at tick 100 can still run out of time
at tick 800.

**Which ticks are slow.** The viewer of the local runner charts what your strategy took to answer each tick against
the 1 s limit, and the game pages on codecraft.day show the same chart for the server's games, with their
`timing.json` to download. Without the viewer, the local runner's batch mode writes that file too. Put a `game.json`
like this in the folder:

```json
{"seed": 7, "game": {"Create": "Round1"},
 "players": [{"Tcp": {"host": "0.0.0.0", "port": 31001, "accept_timeout": null, "timeout": null, "token": null}},
             "QuickStart", "QuickStart", "QuickStart"]}
```

```bash
docker run --rm -p 31001:31001 -v "$PWD":/game ghcr.io/format37/codecraft-runner \
  --config /game/game.json --batch-mode --save-timing /game/timing.json
# or, without Docker: ./codecraft-runner --config game.json --batch-mode --save-timing timing.json
```

Then start your strategy as usual. In `timing.json`, `ms` holds one list per seat with the milliseconds of every
tick from `first_tick` on (`null` for the built-in bots). `"Round2"` plays with fog of war; `"Finals"` is 1v1 and
takes two players.

**Where the time goes.** Profile the slow ticks with your language's profiler. In Python, `tools/profile_tick.py`
does it without touching `main.py` or your strategy: run it instead of `main.py` (the same `host port` follow a
`--`, as in `-- 127.0.0.1 31002`).

```bash
python3 tools/profile_tick.py --ticks 500         # one tick
python3 tools/profile_tick.py --ticks 100 800     # an early and a late tick: what grows in their ncalls?
python3 tools/profile_tick.py --ticks 400-409     # ten ticks in one profile
```

It prints the top functions of each profile, saves it as `profiles/tick_N.prof` (for `snakeviz` or `pstats`) and
the time of every tick to `profiles/ticks.csv`. A profiled tick runs slower than usual. `tools/` is outside the
language folders, so it is never part of a submission.

## Licence

The clients are the official ones from [the original repository](https://github.com/MailRuChamps/raic-2020),
GPL-3.0; this repository keeps that licence ([LICENSE](LICENSE)). The starter strategy is by the CodeCraft
maintainers; do what you like with it.
