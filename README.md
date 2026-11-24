# GTA VI recomp PC status

**Follow the race to run GTA VI on PC, live, from a corner of your screen.**

A dozen open-source projects are racing to get the Xbox Series build of GTA VI running on Windows. This
widget watches all of them and tells you the moment one pulls ahead.

![The panel: milestones of the leader, podium, ranking and latest changes](window/captures/main.png)

## The eye

It sits wherever you drop it. You do not have to look at it: it looks at the race for you.

| Nothing important happened | Something did |
|---|---|
| ![Eye closed](window/captures/eye-closed.png) | ![Eye open, with the number of important changes](window/captures/eye-open.png) |

The eye opens when a project reaches a new milestone, when the leader changes, when a new project appears or
when PortWatch VI publishes an article. A sound plays and the pink badge counts what you have not read yet.
Plain commits are listed in the panel but never open the eye.

## The race

Every project climbs the same ladder, from 0 to 100:

| Milestone | Score | What it means |
|---|---|---|
| Groundwork | 0 to 25 | loader, shader translation and graphics layer, before the game even starts |
| Exe runs | 25 | the game's own code starts running |
| First frame | 40 | logos, a loading screen or the intro |
| Main menu | 55 | the menu is reached and answers the controller |
| In-game | 70 | story mode loads and you can move around |
| 30 FPS | 85 | a 30 FPS average in game |
| Stable | 100 | long sessions without crashing |

The bar at the top of the panel shows where the leader stands on that ladder, the podium shows the top three,
and the list below shows who is chasing them. A half-filled dot means a milestone is only partly reached.

## One project up close

Click any project to see its milestones, the status its author declares, and its latest commits as they land.
Too noisy? One button mutes that project.

![Project page: milestones, summary, latest commits and the mute button](window/captures/project-detail.png)

## Any corner, three languages

The panel grows out of the eye and always unfolds towards the inside of the screen, so the eye can live in any
corner. The interface is in English, French or Spanish.

![Eye in the bottom-right corner, interface in French](window/captures/corner-french.png)

## Using it

- **Click the eye**: unfold or fold the panel.
- **Drag**: move it.
- **The two small strokes by the eye**: drag to resize.
- **Pin button**: keep it above other windows.
- **Dash button**: fold back to the eye when pinned, otherwise hide next to the clock.
- **Right-click** the eye or the tray icon: language, always on top, quit.

Hidden next to the clock, the tray icon is the eye itself: closed or open. A balloon announces important
changes.

Position, size, language and muted projects are saved in `%APPDATA%\GtaPcWatch\settings.ini`.

## How it follows the race

- Every 30 seconds: the commit feed of the three front-runners, and the status table they keep in their README.
- Every 2 minutes: PortWatch VI's data, and the commit feed of every project PortWatch tracks.
- Every 4 minutes: a search for new projects of the same kind.

It only reads public pages. It downloads no game files, needs no account and sends nothing anywhere.

A change of status has to be read twice in a row before it is announced, an empty or truncated answer is
treated as a failed read instead of news, and the same event is never announced twice within ten minutes.

## Building

Windows only. It needs nothing but the C# compiler that ships with .NET Framework 4.x:

```
window\build.cmd
```

This produces `window\GtaPcWatch.exe`. The build runs twice on purpose: the first pass makes the program,
which then draws its own icon, and the second pass embeds that icon.

- `window/GtaPcWatch.cs` is the polling engine.
- `window/EyeWindow.cs` is the window: the eye, the panel, the translations.

## Limits

- The scores are PortWatch VI's judgement, not a measurement. If their site stops updating, the ranking freezes.
- Statuses are what each author declares. Nothing here verifies them.
- Some ranked projects have no public code. They are listed because PortWatch VI ranks them; their page links
  to the PortWatch article instead of a repository, and there are no commits to follow.
- Commit titles, declared statuses and PortWatch texts stay in English whatever the interface language.

## Credits

**[PortWatch VI](https://portwatch-vi.xyz/)** ([source](https://github.com/4lwayz/portwatch-vi)) does the real
work of following this race. This widget relies on it for:

- the milestone ladder and the scoring formula, reproduced here from their `scoring.js`;
- the list of tracked projects and the milestones credited to each;
- the news and the change log shown in the panel.

If you want the full picture, with sources and reasoning for every score, read their site. This is only a
window onto it.

Thanks also to the people building the projects being watched.

## Disclaimer

This is a fan-made status tracker. It is not affiliated with Rockstar Games, Take-Two Interactive, Microsoft,
PortWatch VI or any of the projects it lists. It does not contain, download or help obtain any game file.
Anything offering a ready-made "GTA 6 PC" download is a scam or malware.
