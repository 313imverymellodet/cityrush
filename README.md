# CITY RUSH

Arcade street racing in the browser. Race 5 AI rivals, or up to 3 real players online, across two cities. Each city has its own live leaderboard. It's a Unity 6 WebGL game, runs on phones and desktop, and is hosted on Vercel.

## How to play
- **Gas is automatic.** You only steer.
- **Steer:** slide a thumb on the left half of the screen (keyboard: A/D or the arrow keys).
- **Drift:** hold a hard turn at speed to charge the **NITRO** bar. Tap NITRO (Space) to fire it.
- **Blue chevron pads** give you a free speed boost. **Brake** (S or Down) helps on tight corners.
- A race is 3 laps. Your best time on each city is posted to that city's leaderboard.

## Content
| | |
|---|---|
| Cities | **DOWNTOWN**: dusk, neon towers, long straights and a hairpin. **SUBURBS**: daytime, sweeping bends past houses and trees. |
| Cars | 8 Kenney cars with different speed, acceleration and grip: Velocity, Phantom X, Street GT, Hot Hatch, Intercept, Yellow Cab, Bruiser and Commuter. |
| Solo | 6-car grid. You start mid-pack. AI rivals pass each other, use nitro on straights and slow for corners, and rubber-band a little so races stay close. |
| Online | Quick Race matches racers on the same city. You can also create a private room with a 4-letter code or invite link (`?race=CODE`). Rivals appear as live "ghosts" with name tags. If nobody joins within about 14s, you race the AI instead. |
| Audio | Everything is synthesized at startup: an engine with fake gear shifts, tire squeal, nitro, crashes and a 128 bpm synthwave loop. There are no audio files. |

## Online (Railway, shared with ORBYT: `orbyt/server/race.js`)
| Feature | How it works |
|---|---|
| Leaderboard | `POST /api/race/run` issues a server-timed token at the countdown. `POST /api/race/finish` checks the reported time against the server clock and a minimum lap time for each city. `GET /api/race/board?map=`. New records are pushed to open boards over `/live`. |
| Multiplayer | The `/race` WebSocket hosts rooms of up to 4 racers per city. It relays state at about 12 Hz, orders finishers and sends final results. |
| Moderation | `POST /api/admin/remove` (see the ORBYT README) also removes race times. |

## Layout
```
unity/Assets/Scripts/        Race.cs (game flow, camera, online), Car.cs (physics, AI, remote interpolation),
                             Track.cs (spline road + city generator, minimap), UI.cs, Sfx.cs, Kit.cs, WebBridge.cs
unity/Assets/Plugins/WebGL/  CityRush.jslib: bridge to the page
unity/Assets/WebGLTemplates/CityRush/  loader page, race.js (leaderboard + lobby), OG card, icons
unity/Assets/Editor/         CityBuild.cs (one-command build), KenneyImport.cs
art/                         OG card and icon sources, 3D car renders
dist/                        build output (deploy this)
```

## Build and deploy
```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -projectPath unity -executeMethod CityBuild.WebGL -quit -logFile build.log
node tools/serve.mjs 8080
npx vercel --prod
```
Dev URL flags (only active with `dev=1`): `autodrive=1` lets the AI drive your car (these runs are never submitted), and `fresh=1` resets your save. The public `bot=1` flag is an attract mode that doesn't submit to the leaderboard.

Assets: Kenney Car Kit, City Kit (Commercial, Suburban, Roads) (CC0).
