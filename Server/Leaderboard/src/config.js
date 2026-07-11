import path from "node:path";

function integer(name, fallback, minimum = 0) {
  const value = Number.parseInt(process.env[name] ?? String(fallback), 10);
  if (!Number.isFinite(value) || value < minimum) {
    throw new Error(`${name} must be an integer greater than or equal to ${minimum}`);
  }
  return value;
}

function boolean(name, fallback = false) {
  const value = process.env[name];
  if (value == null) return fallback;
  return value === "true" || value === "1";
}

export function loadConfig(overrides = {}) {
  const config = {
    host: process.env.HOST ?? "127.0.0.1",
    port: integer("PORT", 3100, 1),
    databasePath: process.env.DATABASE_PATH ?? path.resolve("data/leaderboard.sqlite"),
    tokenSecret: process.env.TOKEN_SECRET ?? "development-secret-change-before-production",
    tokenTtlSeconds: integer("TOKEN_TTL_SECONDS", 2_592_000, 60),
    maxScore: integer("MAX_SCORE", 10_000_000, 1),
    minSubmitIntervalSeconds: integer("MIN_SUBMIT_INTERVAL_SECONDS", 2, 0),
    devAuthEnabled: boolean("DEV_AUTH_ENABLED", process.env.NODE_ENV !== "production"),
    wechatAppId: process.env.WECHAT_APP_ID ?? "",
    wechatAppSecret: process.env.WECHAT_APP_SECRET ?? "",
  };

  Object.assign(config, overrides);
  if (process.env.NODE_ENV === "production" && config.tokenSecret.length < 32) {
    throw new Error("TOKEN_SECRET must contain at least 32 characters in production");
  }
  return config;
}
