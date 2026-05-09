# Supabase Setup

Cette version utilise Supabase pour l'authentification et les donnees sociales simples.
Unity communique avec Supabase en REST via `UnityWebRequest`.

## 1. Creer le projet

1. Aller sur `https://supabase.com`.
2. Creer un projet gratuit.
3. Dans `Project Settings > API`, recuperer :
   - `Project URL`;
   - `anon public key`.

Ne jamais utiliser la cle `service_role` dans Unity.

## 2. Configurer les URLs Auth

Dans `Authentication > Providers > Email`, activer `Confirm email` si le professeur veut verifier les emails.

Dans `Authentication > URL Configuration` :

- Site URL : `https://bouzekrimohamed.github.io/quixocomet`
- Redirect URLs : `https://bouzekrimohamed.github.io/quixocomet/reset-password/`

Dans `Authentication > Email Templates > Reset Password`, verifier que le lien de reset utilise une redirection autorisee.

La page web statique est dans :

`docs/reset-password/`

Elle utilise `@supabase/supabase-js@2` via CDN et `supabase.auth.updateUser({ password })`.

## 3. Mettre la config dans Unity

Ouvrir :

`Assets/Scripts/Auth/SupabaseSettings.cs`

Renseigner :

```csharp
public const string ProjectUrl = "https://your-project-ref.supabase.co";
public const string AnonKey = "your-public-anon-key";
public const string PasswordResetRedirectUrl = "https://bouzekrimohamed.github.io/quixocomet/reset-password/";
```

La cle anon est publique cote client. Elle doit quand meme rester differente de la cle `service_role`, qui est interdite dans Unity.

## 4. SQL complet

Executer ce SQL dans `Supabase > SQL Editor`.

```sql
create extension if not exists pgcrypto;

create table if not exists public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  username text unique not null,
  display_name text,
  email text unique,
  created_at timestamp with time zone default now()
);

create table if not exists public.friends (
  id uuid primary key default gen_random_uuid(),
  requester_id uuid references public.profiles(id) on delete cascade,
  receiver_id uuid references public.profiles(id) on delete cascade,
  status text not null default 'pending',
  created_at timestamp with time zone default now(),
  unique(requester_id, receiver_id),
  constraint friends_no_self_request check (requester_id <> receiver_id),
  constraint friends_status_check check (status in ('pending', 'accepted', 'rejected', 'blocked'))
);

create unique index if not exists friends_unique_pair
on public.friends (
  least(requester_id, receiver_id),
  greatest(requester_id, receiver_id)
);

alter table public.profiles enable row level security;
alter table public.friends enable row level security;

drop policy if exists "profiles_select_authenticated" on public.profiles;
drop policy if exists "profiles_select_public_login" on public.profiles;
drop policy if exists "profiles_insert_own" on public.profiles;
drop policy if exists "profiles_update_own" on public.profiles;

create policy "profiles_select_public_login"
on public.profiles
for select
to anon, authenticated
using (true);

create policy "profiles_insert_own"
on public.profiles
for insert
to authenticated
with check (auth.uid() = id);

create policy "profiles_update_own"
on public.profiles
for update
to authenticated
using (auth.uid() = id)
with check (auth.uid() = id);

drop policy if exists "friends_select_own" on public.friends;
drop policy if exists "friends_insert_own_pending" on public.friends;
drop policy if exists "friends_update_received_pending" on public.friends;

create policy "friends_select_own"
on public.friends
for select
to authenticated
using (
  auth.uid() = requester_id
  or auth.uid() = receiver_id
);

create policy "friends_insert_own_pending"
on public.friends
for insert
to authenticated
with check (
  auth.uid() = requester_id
  and status = 'pending'
);

create policy "friends_update_received_pending"
on public.friends
for update
to authenticated
using (
  auth.uid() = receiver_id
  and status = 'pending'
)
with check (
  auth.uid() = receiver_id
  and status in ('accepted', 'rejected', 'blocked')
);
```

## 5. Migration si profiles existe deja

Si `profiles` existe deja sans colonne email, executer :

```sql
alter table public.profiles add column if not exists email text unique;

drop policy if exists "profiles_select_authenticated" on public.profiles;
drop policy if exists "profiles_select_public_login" on public.profiles;

create policy "profiles_select_public_login"
on public.profiles
for select
to anon, authenticated
using (true);
```

## 6. Notes RLS

La policy `profiles_select_public_login` autorise la lecture publique de `profiles` pour permettre le login par username avant authentification.
Pour cette V1 etudiante, la table ne contient pas de mot de passe.
Elle contient `id`, `username`, `display_name`, `email`, `created_at`.

Les mots de passe ne sont jamais stockes dans `profiles`.
Ils sont geres uniquement par Supabase Auth.

Pour une V2 plus stricte, remplacer cette lecture publique par une RPC dediee qui retourne uniquement l'email associe au username.

## 7. Tests attendus

1. Generer les scenes Unity.
2. Ouvrir `IntroVideoScene`.
3. Play.
4. Inscription avec email, mot de passe et username.
5. Connexion avec email + mot de passe.
6. Deconnexion.
7. Connexion avec username + mot de passe.
8. Cliquer `Mot de passe oublie` avec un email.
9. Ouvrir le lien recu et changer le mot de passe.
10. Tester les amis.

## 8. Si Supabase n'est pas configure

Le jeu ne doit pas crasher.
`AuthScene` affiche un message et permet `Continuer hors ligne`.
Les amis restent indisponibles en mode hors ligne.
