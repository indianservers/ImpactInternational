import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: "127.0.0.1",
    port: 1233,
    proxy: {
      "/api": {target:process.env.CLUBS_API_TARGET || "http://127.0.0.1:5080",ws:true},
      "/health": process.env.CLUBS_API_TARGET || "http://127.0.0.1:5080",
    },
  },
});
