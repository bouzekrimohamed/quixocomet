// Modifier uniquement ces valeurs quand une nouvelle release est publiee.
const DOWNLOADS = {
  windows: "https://github.com/bouzekrimohamed/quixocomet/releases/download/v1.0.0/BuildWindows.zip",
  linux: "https://github.com/bouzekrimohamed/quixocomet/releases/download/v1.0.0/BuildLinux.zip",
  macos: "https://github.com/bouzekrimohamed/quixocomet/releases/download/v1.0.0/BuildMacOS.zip"
};

document.querySelectorAll("[data-download]").forEach((button) => {
  const url = DOWNLOADS[button.dataset.download];
  if (url) {
    button.href = url;
    return;
  }

  button.removeAttribute("href");
  button.textContent = "Bientôt disponible";
  button.classList.add("disabled");
  button.setAttribute("aria-disabled", "true");
});
