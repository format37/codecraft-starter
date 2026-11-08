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

Submitting again under the same strategy name makes a new version; the latest successful build plays.

## Make it yours

Fork, change the strategy file of your language, push, submit the fork's URL. The rules of the game and the
whole entity model are in the [server repository](https://github.com/format37/codecraft): `docs/doc-en.md`.

To test on your machine, use the local runner from the same repository. It plays the server's games (the same
engine, rules and maps) live in the browser, with the debug interface the strategy draws and logs through:

```bash
git clone https://github.com/format37/codecraft && make -C codecraft/sim-cpp
codecraft/sim-cpp/build/codecraft-sim-cpp     # pick the game and the seats in the browser
```

Then start your strategy: each client's runner takes `host port` (and an optional token) and connects to
`127.0.0.1 31001` by default; in Python that is `python3 main.py`.

Like the server, the runner cuts off a strategy that takes more than 1 s to answer a tick, or 40 s in all. To
stop at breakpoints in a debugger, start it with `--limits warn` (or pick that in the viewer's side panel): it
then waits as long as needed and only marks the ticks the server would not allow.

Only the strategy file is yours to edit: the rest of each folder is the client, and the server builds every
folder exactly like the 2020 contest did (`compile.sh` of the official client, then `run.sh`).

## Licence

The clients are the official ones from [the original repository](https://github.com/MailRuChamps/raic-2020),
GPL-3.0; this repository keeps that licence ([LICENSE](LICENSE)). The starter strategy is by the CodeCraft
maintainers; do what you like with it.
