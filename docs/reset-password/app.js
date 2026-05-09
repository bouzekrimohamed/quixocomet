const SUPABASE_URL = "https://wcwufabumabolxhmpexc.supabase.co";
const SUPABASE_ANON_KEY = "sb_publishable_PwbgvZXpUn07HsvFRghnPg_R_9T5W3H";

const client = supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
  auth: {
    detectSessionInUrl: true,
    persistSession: false,
    autoRefreshToken: false
  }
});

const form = document.querySelector("#reset-form");
const passwordInput = document.querySelector("#password");
const confirmInput = document.querySelector("#confirm-password");
const message = document.querySelector("#message");
const submitButton = document.querySelector("#submit-button");

initRecoverySession();

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  setMessage("", "");

  const password = passwordInput.value;
  const confirm = confirmInput.value;

  if (!password || !confirm) {
    setMessage("Veuillez remplir les deux champs.", "error");
    return;
  }

  if (password.length < 6) {
    setMessage("Le mot de passe doit contenir au moins 6 caractères.", "error");
    return;
  }

  if (password !== confirm) {
    setMessage("Les deux mots de passe ne correspondent pas.", "error");
    return;
  }

  submitButton.disabled = true;
  try {
    const { error } = await client.auth.updateUser({ password });
    if (error) {
      setMessage(error.message || "Impossible de mettre à jour le mot de passe.", "error");
      return;
    }

    passwordInput.value = "";
    confirmInput.value = "";
    setMessage("Mot de passe mis à jour avec succès. Vous pouvez retourner dans le jeu.", "success");
  } catch {
    setMessage("Connexion au service impossible. Réessayez dans un instant.", "error");
  } finally {
    submitButton.disabled = false;
  }
});

async function initRecoverySession() {
  const hash = new URLSearchParams(window.location.hash.replace(/^#/, ""));
  const query = new URLSearchParams(window.location.search);
  const accessToken = hash.get("access_token") || query.get("access_token");
  const refreshToken = hash.get("refresh_token") || query.get("refresh_token");
  const code = query.get("code");

  if (accessToken && refreshToken) {
    await client.auth.setSession({
      access_token: accessToken,
      refresh_token: refreshToken
    });

    window.history.replaceState({}, document.title, window.location.pathname);
    return;
  }

  if (code) {
    await client.auth.exchangeCodeForSession(code);
    window.history.replaceState({}, document.title, window.location.pathname);
    return;
  }

  await client.auth.getSession();
}

function setMessage(text, type) {
  message.textContent = text;
  message.className = type ? `message ${type}` : "message";
}
