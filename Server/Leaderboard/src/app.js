import { LeaderboardDatabase } from "./database.js";
import { issueToken, verifyToken } from "./token.js";

function publicPlayer(player) {
  return {
    id: player.id,
    displayName: player.display_name,
    bestScore: player.best_score,
    ...(player.rank == null ? {} : { rank: player.rank }),
  };
}

function parseBearer(request) {
  const value = request.headers.authorization;
  return typeof value === "string" && value.startsWith("Bearer ") ? value.slice(7) : null;
}

async function exchangeWechatCode(code, config) {
  const url = new URL("https://api.weixin.qq.com/sns/jscode2session");
  url.searchParams.set("appid", config.wechatAppId);
  url.searchParams.set("secret", config.wechatAppSecret);
  url.searchParams.set("js_code", code);
  url.searchParams.set("grant_type", "authorization_code");
  const response = await fetch(url, { signal: AbortSignal.timeout(5000) });
  const result = await response.json();
  if (!response.ok || !result.openid) throw new Error(result.errmsg ?? "WeChat login failed");
  return result.openid;
}

export async function buildApp(fastify, config, options = {}) {
  const database = options.database ?? new LeaderboardDatabase(config.databasePath);
  const wechatExchange = options.wechatExchange ?? exchangeWechatCode;

  fastify.decorateRequest("player", null);
  fastify.addHook("onClose", async () => {
    if (!options.database) database.close();
  });

  async function authenticate(request, reply) {
    const claims = verifyToken(parseBearer(request), config);
    const player = claims ? database.getPlayer(claims.sub) : null;
    if (!player) return reply.code(401).send({ error: "unauthorized" });
    request.player = player;
  }

  fastify.get("/health", async () => ({ status: "ok" }));

  fastify.post("/api/v1/auth/dev", async (request, reply) => {
    if (!config.devAuthEnabled) return reply.code(404).send({ error: "not_found" });
    const playerId = request.body?.playerId;
    if (typeof playerId !== "string" || !/^[a-zA-Z0-9_-]{3,64}$/.test(playerId)) {
      return reply.code(400).send({ error: "invalid_player_id" });
    }
    const player = database.getOrCreatePlayer("dev", playerId);
    return { token: issueToken(player.id, config), player: publicPlayer(player) };
  });

  fastify.post("/api/v1/auth/wechat", async (request, reply) => {
    if (!config.wechatAppId || !config.wechatAppSecret) {
      return reply.code(503).send({ error: "wechat_auth_not_configured" });
    }
    const code = request.body?.code;
    if (typeof code !== "string" || code.length < 3 || code.length > 256) {
      return reply.code(400).send({ error: "invalid_code" });
    }
    try {
      const openid = await wechatExchange(code, config);
      const player = database.getOrCreatePlayer("wechat", openid);
      return { token: issueToken(player.id, config), player: publicPlayer(player) };
    } catch (error) {
      request.log.warn({ error }, "WeChat login failed");
      return reply.code(401).send({ error: "wechat_login_failed" });
    }
  });

  fastify.post("/api/v1/scores", { preHandler: authenticate }, async (request, reply) => {
    const body = request.body ?? {};
    const valid =
      typeof body.runId === "string" && /^[a-zA-Z0-9_-]{8,80}$/.test(body.runId) &&
      Number.isInteger(body.score) && body.score >= 0 && body.score <= config.maxScore &&
      Number.isInteger(body.durationSeconds) && body.durationSeconds >= 0 && body.durationSeconds <= 86_400 &&
      Number.isInteger(body.clears) && body.clears >= 0 && body.clears <= 100_000 &&
      Number.isInteger(body.maxCombo) && body.maxCombo >= 0 && body.maxCombo <= 10_000;
    if (!valid) return reply.code(400).send({ error: "invalid_submission" });

    const now = Math.floor(Date.now() / 1000);
    const result = database.submitScore(request.player.id, body, now, config.minSubmitIntervalSeconds);
    if (result.status === "rate-limited") return reply.code(429).send({ error: "rate_limited" });
    if (result.status === "run-conflict") return reply.code(409).send({ error: "run_id_conflict" });
    if (result.status === "missing-player") return reply.code(401).send({ error: "unauthorized" });
    return {
      accepted: result.status === "accepted",
      duplicate: result.status === "duplicate",
      player: publicPlayer(result.player),
    };
  });

  fastify.get("/api/v1/leaderboard", { preHandler: authenticate }, async (request) => {
    const parsed = Number.parseInt(request.query?.limit ?? "50", 10);
    const limit = Number.isFinite(parsed) ? Math.min(Math.max(parsed, 1), 100) : 50;
    const result = database.leaderboard(limit, request.player.id);
    return {
      players: result.players.map(publicPlayer),
      me: result.me ? publicPlayer(result.me) : null,
    };
  });

  return fastify;
}
