import Fastify from "fastify";
import { buildApp } from "./app.js";
import { loadConfig } from "./config.js";

const config = loadConfig();
const app = Fastify({ logger: true, bodyLimit: 16 * 1024 });
await buildApp(app, config);
await app.listen({ host: config.host, port: config.port });
