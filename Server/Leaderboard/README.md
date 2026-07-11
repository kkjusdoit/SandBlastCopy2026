# FlowSand Leaderboard

## Local development

```bash
npm install
npm start
```

Development login is enabled outside production. Unity Editor connects to
`http://127.0.0.1:3100` by default.

## Tests

```bash
npm test
```

## Production

Copy `.env.example` to `/srv/sandrussia/shared/leaderboard.env`, set a random
`TOKEN_SECRET`, configure the WeChat credentials, and keep
`DEV_AUTH_ENABLED=false`. Deploy with:

```bash
./deploy/deploy.sh
```

The production API listens on `127.0.0.1:3100` and is exposed through Nginx.
HTTPS still requires a real domain, ICP registration, and its certificate.
