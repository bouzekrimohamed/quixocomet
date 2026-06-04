// Remplacer uniquement ces trois valeurs quand les archives sont publiées.
const DOWNLOADS = {
  windows: "WINDOWS_DOWNLOAD_URL",
  linux: "LINUX_DOWNLOAD_URL",
  macos: "MACOS_DOWNLOAD_URL"
};

document.querySelectorAll("[data-download]").forEach((button) => {
  const url = DOWNLOADS[button.dataset.download];
  button.href = url;
  button.addEventListener("click", (event) => {
    if (!url || url.endsWith("_DOWNLOAD_URL")) {
      event.preventDefault();
      alert("Ce téléchargement sera bientôt disponible.");
    }
  });
});
