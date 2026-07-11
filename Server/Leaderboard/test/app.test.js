import assert from "node:assert/strict";
import test from "node:test";
import Fastify from "fastify";
import { buildApp } from "../src/app.js";
import { loadConfig } from "../src/config.js";
import { LeaderboardDatabase } from "../src/database.js";

async function fixture(overrides = {}) {
  const app = Fastify();
  const database = new LeaderboardDatabase(":memory:");
  const config = loadConfig({
    databasePath: ":memory:",
    tokenSecret: "test-secret-that-is-long-enough-for-tests",
    devAuthEnabled: true,
    minSubmitIntervalSeconds: 0,
    ...overrides,
  });
  await buildApp(app, config, { database, wechatExchange: async () => "wechat-openid" });
  return { app, database };
}

async function login(app, playerId = "editor-player") {
  const response = await app.inject({ method: "POST", url: "/api/v1/auth/dev", payload: { playerId } });
  assert.equal(response.statusCode, 200);
  return response.json();
}

test("health endpoint reports readiness", async (t) => {
  const { app, database } = await fixture();
  t.after(() => { database.close(); app.close(); });
  const response = await app.inject({ method: "GET", url: "/health" });
  assert.deepEqual(response.json(), { status: "ok" });
});

test("score submission keeps the highest score and is idempotent", async (t) => {
  const { app, database } = await fixture();
  t.after(() => { database.close(); app.close(); });
  const auth = await login(app);
  const headers = { authorization: `Bearer ${auth.token}` };
  const first = await app.inject({
    method: "POST", url: "/api/v1/scores", headers,
    payload: { runId: "run-00000001", score: 120, durationSeconds: 40, clears: 3, maxCombo: 2 },
  });
  assert.equal(first.statusCode, 200);
  assert.equal(first.json().player.bestScore, 120);

  const duplicate = await app.inject({
    method: "POST", url: "/api/v1/scores", headers,
    payload: { runId: "run-00000001", score: 999, durationSeconds: 40, clears: 3, maxCombo: 2 },
  });
  assert.equal(duplicate.json().duplicate, true);
  assert.equal(duplicate.json().player.bestScore, 120);

  const lower = await app.inject({
    method: "POST", url: "/api/v1/scores", headers,
    payload: { runId: "run-00000002", score: 80, durationSeconds: 20, clears: 1, maxCombo: 1 },
  });
  assert.equal(lower.json().player.bestScore, 120);
});

test("leaderboard is ordered and includes the current player rank", async (t) => {
  const { app, database } = await fixture();
  t.after(() => { database.close(); app.close(); });
  const first = await login(app, "first-player");
  const second = await login(app, "second-player");
  for (const [auth, runId, score] of [[first, "run-first-001", 50], [second, "run-second-01", 100]]) {
    await app.inject({
      method: "POST", url: "/api/v1/scores",
      headers: { authorization: `Bearer ${auth.token}` },
      payload: { runId, score, durationSeconds: 30, clears: 1, maxCombo: 1 },
    });
  }
  const response = await app.inject({
    method: "GET", url: "/api/v1/leaderboard?limit=50",
    headers: { authorization: `Bearer ${first.token}` },
  });
  const body = response.json();
  assert.deepEqual(body.players.map((player) => player.bestScore), [100, 50]);
  assert.equal(body.me.rank, 2);
});

test("production can disable development login", async (t) => {
  const { app, database } = await fixture({ devAuthEnabled: false });
  t.after(() => { database.close(); app.close(); });
  const response = await app.inject({ method: "POST", url: "/api/v1/auth/dev", payload: { playerId: "editor-player" } });
  assert.equal(response.statusCode, 404);
});
