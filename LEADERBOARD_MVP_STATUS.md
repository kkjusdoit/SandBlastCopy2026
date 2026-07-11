# Leaderboard MVP Status

This branch contains an experimental first version of the global leaderboard.
It is intentionally not part of the first production release.

## Implemented

### Server

- Fastify API with SQLite persistence.
- Development and WeChat authentication endpoints.
- Signed bearer tokens with expiry.
- Score validation, per-run idempotency, rate limiting, and highest-score-only updates.
- Top leaderboard and current-player rank endpoints.
- Automated Node tests for health, score submission, ordering, idempotency, and disabled production dev login.

### Unity client

- Shared API client and leaderboard models for Editor and WeChat builds.
- Unity Editor development identity.
- `WX.Login` flow for WeChat builds.
- Token caching and single-flight login.
- Game-over score submission with offline retry queue.
- Runtime leaderboard panel showing the top ten and the current player.
- Automatic pause while the leaderboard is open.
- Loading, empty, unavailable, and close states.

### UI and fonts

- Runtime leaderboard copy and portrait layout.
- WebGL default canvas changed to 540x960 for portrait previews.
- Noto Sans SC subset and TMP SDF rebuilt for the new copy.
- Font validation command checks all required characters.

### Deployment

- systemd service with restricted filesystem access and automatic restart.
- Nginx reverse proxy and request limiting.
- Daily SQLite backup timer with 14-day retention.
- Versioned rsync/SSH deployment script.
- Experimental API deployed to the current ECS and verified through `/health`.
- Production development login is disabled on the ECS.

## Verification completed

- Server tests: 4 passed, 0 failed.
- Unity Editor compilation: passed.
- Unity WebGL/WeChat conditional compilation: passed.
- Full WebGL build: passed.
- Leaderboard-specific Unity EditMode tests: 2 passed.
- Runtime UI visually checked at 540x960 and 360x800 without panel clipping or overlap.
- Font subset: 144 required characters validated.
- ECS services `flowsand-leaderboard`, `nginx`, and `fail2ban`: active.

## Known issues and limitations

- The project-wide EditMode suite currently reports 18 passed and 2 failed. The two failures are pre-existing gameplay tests:
  - `LockedBridgeWaitsForSandSimulationBeforeClearDetection`
  - `SpeedIncreasesOnceEveryThirtySecondsAndStopsAtLevelTen`
- The production Unity API URL remains the placeholder `https://api.example.com`.
- The ECS endpoint currently has HTTP health access only; it is not a valid WeChat request domain.
- WeChat authentication is deployed but intentionally unconfigured.
- Anti-cheat validation is basic and does not replay or verify game events.
- Player profiles are anonymous; there is no nickname/avatar editing flow.
- No season reset, pagination UI, moderation, or administrative tooling exists.

## Required before revisiting for release

1. Obtain and register a real API domain.
2. Complete ICP filing for the mainland China ECS deployment.
3. Configure DNS and an HTTPS certificate.
4. Add the HTTPS domain to the WeChat Mini Game request-domain allowlist.
5. Set `WECHAT_APP_ID` and `WECHAT_APP_SECRET` in `/srv/sandrussia/shared/leaderboard.env` without committing them.
6. Replace the production URL in `FlowSandEnvironment`.
7. Perform login, submission, retry, and ranking tests in WeChat DevTools and on a physical device.
8. Decide whether anonymous profiles are sufficient or implement opt-in avatar/nickname profiles.
9. Define a stronger anti-cheat policy and suspicious-score review flow.
10. Resolve or explicitly baseline the two existing gameplay test failures.
11. Add database restore rehearsal and monitoring/alerting.
12. Review privacy copy and data-retention policy before collecting real player identifiers.

## Local development

```bash
cd Server/Leaderboard
npm install
npm start
```

Unity Editor connects to `http://127.0.0.1:3100` by default. The local server enables development login unless `NODE_ENV=production` or `DEV_AUTH_ENABLED=false` is set.

## Deployment reference

See `Server/Leaderboard/README.md`. Deployment defaults to the SSH alias `aliyun` and keeps the database and environment file under `/srv/sandrussia/shared`.
