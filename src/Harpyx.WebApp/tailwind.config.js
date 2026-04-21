/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.cshtml",
    "./Views/**/*.cshtml",
    "./Controllers/**/*.cs",
    "./**/*.cshtml"
  ],
  theme: {
    extend: {}
  },
  plugins: [require("daisyui")],
  daisyui: {
    themes: [
      "aqua",
      "black",
      "bumblebee",
      "cmyk",
      "corporate",
      "cupcake",
      "cyberpunk",
      "dark",
      "dracula",
      "emerald",
      "fantasy",
      "forest",
      "garden",
      "halloween",
      "light",
      "lofi",
      "luxury",
      "pastel",
      "retro",
      "synthwave",
      "valentine",
      "wireframe",
      "autumn",
      "business",
      "acid",
      "lemonade",
      "night",
      "coffee",
      "winter",
      "dim",
      "nord",
      "sunset"
    ]
  }
};