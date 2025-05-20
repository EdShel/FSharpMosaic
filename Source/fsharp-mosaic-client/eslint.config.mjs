import { dirname } from "path";
import { fileURLToPath } from "url";
import { FlatCompat } from "@eslint/eslintrc";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  ...compat.config({
    rules: {
      // Mostly disabled for useEffect hooks: they must run once (i.e. with empty deps array), or twice in debug mode
      "react-hooks/exhaustive-deps": "off",
    },
  }),
];

export default eslintConfig;
