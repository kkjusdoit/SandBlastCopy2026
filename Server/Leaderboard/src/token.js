import crypto from "node:crypto";

function encode(value) {
  return Buffer.from(value).toString("base64url");
}

function signature(payload, secret) {
  return crypto.createHmac("sha256", secret).update(payload).digest("base64url");
}

export function issueToken(playerId, config, nowSeconds = Math.floor(Date.now() / 1000)) {
  const payload = encode(JSON.stringify({ sub: playerId, iat: nowSeconds, exp: nowSeconds + config.tokenTtlSeconds }));
  return `${payload}.${signature(payload, config.tokenSecret)}`;
}

export function verifyToken(token, config, nowSeconds = Math.floor(Date.now() / 1000)) {
  if (typeof token !== "string") return null;
  const [payload, providedSignature, extra] = token.split(".");
  if (!payload || !providedSignature || extra) return null;

  const expected = signature(payload, config.tokenSecret);
  const left = Buffer.from(providedSignature);
  const right = Buffer.from(expected);
  if (left.length !== right.length || !crypto.timingSafeEqual(left, right)) return null;

  try {
    const claims = JSON.parse(Buffer.from(payload, "base64url").toString("utf8"));
    if (typeof claims.sub !== "string" || claims.exp < nowSeconds) return null;
    return claims;
  } catch {
    return null;
  }
}
