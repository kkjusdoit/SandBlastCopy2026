import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import Database from "better-sqlite3";

const schema = `
CREATE TABLE IF NOT EXISTS players (
  id TEXT PRIMARY KEY,
  provider TEXT NOT NULL,
  provider_user_id TEXT NOT NULL,
  display_name TEXT NOT NULL,
  best_score INTEGER NOT NULL DEFAULT 0,
  last_score_at INTEGER,
  created_at INTEGER NOT NULL,
  updated_at INTEGER NOT NULL,
  UNIQUE(provider, provider_user_id)
);

CREATE TABLE IF NOT EXISTS score_submissions (
  run_id TEXT PRIMARY KEY,
  player_id TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  score INTEGER NOT NULL,
  duration_seconds INTEGER NOT NULL,
  clears INTEGER NOT NULL,
  max_combo INTEGER NOT NULL,
  created_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS players_score_idx
ON players(best_score DESC, updated_at ASC);
`;

function anonymousName(id) {
  return `玩家${id.replaceAll("-", "").slice(0, 4).toUpperCase()}`;
}

export class LeaderboardDatabase {
  constructor(filename) {
    if (filename !== ":memory:") fs.mkdirSync(path.dirname(filename), { recursive: true });
    this.db = new Database(filename);
    this.db.pragma("journal_mode = WAL");
    this.db.pragma("foreign_keys = ON");
    this.db.exec(schema);
    this.prepare();
  }

  prepare() {
    this.findIdentity = this.db.prepare("SELECT * FROM players WHERE provider = ? AND provider_user_id = ?");
    this.findPlayer = this.db.prepare("SELECT * FROM players WHERE id = ?");
    this.insertPlayer = this.db.prepare(`
      INSERT INTO players (id, provider, provider_user_id, display_name, created_at, updated_at)
      VALUES (?, ?, ?, ?, ?, ?)
    `);
    this.findRun = this.db.prepare("SELECT player_id, score FROM score_submissions WHERE run_id = ?");
    this.insertRun = this.db.prepare(`
      INSERT INTO score_submissions (run_id, player_id, score, duration_seconds, clears, max_combo, created_at)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `);
    this.updateBest = this.db.prepare(`
      UPDATE players
      SET best_score = MAX(best_score, ?), last_score_at = ?, updated_at = ?
      WHERE id = ?
    `);
    this.topPlayers = this.db.prepare(`
      SELECT id, display_name, best_score,
        ROW_NUMBER() OVER (ORDER BY best_score DESC, updated_at ASC) AS rank
      FROM players
      WHERE best_score > 0
      ORDER BY best_score DESC, updated_at ASC
      LIMIT ?
    `);
    this.playerRank = this.db.prepare(`
      SELECT id, display_name, best_score, rank FROM (
        SELECT id, display_name, best_score,
          ROW_NUMBER() OVER (ORDER BY best_score DESC, updated_at ASC) AS rank
        FROM players WHERE best_score > 0
      ) WHERE id = ?
    `);
    this.submitTransaction = this.db.transaction((playerId, submission, now, minimumInterval) => {
      const player = this.findPlayer.get(playerId);
      if (!player) return { status: "missing-player" };
      const existing = this.findRun.get(submission.runId);
      if (existing) {
        return existing.player_id === playerId
          ? { status: "duplicate", player: this.findPlayer.get(playerId) }
          : { status: "run-conflict" };
      }
      if (player.last_score_at != null && now - player.last_score_at < minimumInterval) {
        return { status: "rate-limited" };
      }
      this.insertRun.run(
        submission.runId,
        playerId,
        submission.score,
        submission.durationSeconds,
        submission.clears,
        submission.maxCombo,
        now,
      );
      this.updateBest.run(submission.score, now, now, playerId);
      return { status: "accepted", player: this.findPlayer.get(playerId) };
    });
  }

  getOrCreatePlayer(provider, providerUserId, now = Math.floor(Date.now() / 1000)) {
    let player = this.findIdentity.get(provider, providerUserId);
    if (player) return player;
    const id = crypto.randomUUID();
    this.insertPlayer.run(id, provider, providerUserId, anonymousName(id), now, now);
    return this.findPlayer.get(id);
  }

  getPlayer(id) {
    return this.findPlayer.get(id) ?? null;
  }

  submitScore(playerId, submission, now, minimumInterval) {
    return this.submitTransaction(playerId, submission, now, minimumInterval);
  }

  leaderboard(limit, playerId) {
    return {
      players: this.topPlayers.all(limit),
      me: playerId ? this.playerRank.get(playerId) ?? null : null,
    };
  }

  close() {
    this.db.close();
  }
}
