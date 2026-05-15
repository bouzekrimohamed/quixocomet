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

## 9. Online Multiplayer

Cette V1 online utilise Supabase REST avec polling :

- presence heartbeat toutes les 10 secondes;
- refresh amis/invitations environ toutes les 5 secondes;
- polling des matchs online toutes les 1 seconde;
- pas de cle `service_role` dans Unity.

Executer cette migration apres le SQL de base profils/amis.

```sql
create extension if not exists pgcrypto;

create table if not exists public.user_presence (
  user_id uuid primary key references public.profiles(id) on delete cascade,
  username text,
  status text not null default 'online',
  last_seen_at timestamptz not null default now(),
  constraint user_presence_status_check check (status in ('online', 'offline'))
);

create table if not exists public.match_invites (
  id uuid primary key default gen_random_uuid(),
  from_user_id uuid not null references public.profiles(id) on delete cascade,
  to_user_id uuid not null references public.profiles(id) on delete cascade,
  game_kind text not null,
  status text not null default 'pending',
  match_id uuid,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint match_invites_game_kind_check check (game_kind in ('Quixo', 'Qomet')),
  constraint match_invites_status_check check (status in ('pending', 'accepted', 'rejected', 'cancelled', 'expired')),
  constraint match_invites_no_self_check check (from_user_id <> to_user_id)
);

create table if not exists public.matchmaking_queue (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references public.profiles(id) on delete cascade,
  game_kind text not null,
  status text not null default 'waiting',
  match_id uuid,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique(user_id, game_kind),
  constraint matchmaking_game_kind_check check (game_kind in ('Quixo', 'Qomet')),
  constraint matchmaking_status_check check (status in ('waiting', 'matched', 'cancelled'))
);

create table if not exists public.online_matches (
  id uuid primary key default gen_random_uuid(),
  game_kind text not null,
  player1_id uuid not null references public.profiles(id) on delete cascade,
  player2_id uuid not null references public.profiles(id) on delete cascade,
  current_turn_id uuid not null references public.profiles(id),
  status text not null default 'active',
  winner_id uuid references public.profiles(id),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint online_matches_game_kind_check check (game_kind in ('Quixo', 'Qomet')),
  constraint online_matches_status_check check (status in ('active', 'finished', 'cancelled')),
  constraint online_matches_no_self_check check (player1_id <> player2_id),
  constraint online_matches_turn_player_check check (current_turn_id in (player1_id, player2_id)),
  constraint online_matches_winner_player_check check (winner_id is null or winner_id in (player1_id, player2_id))
);

alter table public.match_invites
  drop constraint if exists match_invites_match_id_fkey;
alter table public.match_invites
  add constraint match_invites_match_id_fkey
  foreign key (match_id) references public.online_matches(id) on delete set null;

alter table public.matchmaking_queue
  drop constraint if exists matchmaking_queue_match_id_fkey;
alter table public.matchmaking_queue
  add constraint matchmaking_queue_match_id_fkey
  foreign key (match_id) references public.online_matches(id) on delete set null;

create table if not exists public.online_moves (
  id uuid primary key default gen_random_uuid(),
  match_id uuid not null references public.online_matches(id) on delete cascade,
  player_id uuid not null references public.profiles(id) on delete cascade,
  move_number int not null,
  move_payload jsonb not null,
  created_at timestamptz not null default now(),
  unique(match_id, move_number)
);

alter table public.user_presence enable row level security;
alter table public.match_invites enable row level security;
alter table public.matchmaking_queue enable row level security;
alter table public.online_matches enable row level security;
alter table public.online_moves enable row level security;

drop policy if exists "presence_select_authenticated" on public.user_presence;
drop policy if exists "presence_insert_own" on public.user_presence;
drop policy if exists "presence_update_own" on public.user_presence;

create policy "presence_select_authenticated"
on public.user_presence
for select
to authenticated
using (true);

create policy "presence_insert_own"
on public.user_presence
for insert
to authenticated
with check (auth.uid() = user_id);

create policy "presence_update_own"
on public.user_presence
for update
to authenticated
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "match_invites_select_own" on public.match_invites;
drop policy if exists "match_invites_insert_own" on public.match_invites;
drop policy if exists "match_invites_update_receiver" on public.match_invites;
drop policy if exists "match_invites_cancel_sender" on public.match_invites;

create policy "match_invites_select_own"
on public.match_invites
for select
to authenticated
using (auth.uid() = from_user_id or auth.uid() = to_user_id);

create policy "match_invites_insert_own"
on public.match_invites
for insert
to authenticated
with check (auth.uid() = from_user_id and status = 'pending');

create policy "match_invites_update_receiver"
on public.match_invites
for update
to authenticated
using (auth.uid() = to_user_id and status = 'pending')
with check (auth.uid() = to_user_id and status in ('accepted', 'rejected', 'expired'));

create policy "match_invites_cancel_sender"
on public.match_invites
for update
to authenticated
using (auth.uid() = from_user_id and status = 'pending')
with check (auth.uid() = from_user_id and status = 'cancelled');

drop policy if exists "matchmaking_select_waiting_or_own" on public.matchmaking_queue;
drop policy if exists "matchmaking_insert_own" on public.matchmaking_queue;
drop policy if exists "matchmaking_update_own_or_claim_waiting" on public.matchmaking_queue;

create policy "matchmaking_select_waiting_or_own"
on public.matchmaking_queue
for select
to authenticated
using (auth.uid() = user_id or status = 'waiting');

create policy "matchmaking_insert_own"
on public.matchmaking_queue
for insert
to authenticated
with check (auth.uid() = user_id);

create policy "matchmaking_update_own_or_claim_waiting"
on public.matchmaking_queue
for update
to authenticated
using (auth.uid() = user_id or status = 'waiting')
with check (auth.uid() = user_id or status = 'matched');

drop policy if exists "online_matches_select_participants" on public.online_matches;
drop policy if exists "online_matches_insert_participant" on public.online_matches;
drop policy if exists "online_matches_update_participants" on public.online_matches;

create policy "online_matches_select_participants"
on public.online_matches
for select
to authenticated
using (auth.uid() = player1_id or auth.uid() = player2_id);

create policy "online_matches_insert_participant"
on public.online_matches
for insert
to authenticated
with check (
  auth.uid() in (player1_id, player2_id)
  and current_turn_id = player1_id
  and status = 'active'
);

create policy "online_matches_update_participants"
on public.online_matches
for update
to authenticated
using (auth.uid() in (player1_id, player2_id))
with check (auth.uid() in (player1_id, player2_id));

drop policy if exists "online_moves_select_match_participants" on public.online_moves;
drop policy if exists "online_moves_insert_current_turn" on public.online_moves;

create policy "online_moves_select_match_participants"
on public.online_moves
for select
to authenticated
using (
  exists (
    select 1
    from public.online_matches m
    where m.id = online_moves.match_id
      and auth.uid() in (m.player1_id, m.player2_id)
  )
);

create policy "online_moves_insert_current_turn"
on public.online_moves
for insert
to authenticated
with check (
  auth.uid() = player_id
  and exists (
    select 1
    from public.online_matches m
    where m.id = online_moves.match_id
      and m.status = 'active'
      and m.current_turn_id = auth.uid()
      and auth.uid() in (m.player1_id, m.player2_id)
  )
);
```

### Tester presence

1. Lancer deux comptes Supabase amis.
2. Ouvrir `Amis`.
3. Le compte actif envoie un heartbeat dans `user_presence`.
4. Un ami avec `last_seen_at` plus recent que 30 secondes apparait online.

### Tester invitation ami

1. Les deux comptes doivent etre amis acceptes.
2. Compte A ouvre `Amis` puis clique `Quixo` ou `Qomet` sur la ligne de B.
3. Compte B voit l'invitation recue et clique `Accepter`.
4. Les deux chargent `GameplayScene` en online.

### Tester matchmaking

1. Compte A clique `Jouer Qomet en ligne`.
2. Compte B clique `Jouer Qomet en ligne`.
3. Le premier entre en attente, le deuxieme cree le match, puis les deux chargent la partie.

### Limitations V1 online

La V1 est client-authoritative : Unity valide les coups localement et Supabase controle surtout l'identite, les droits RLS et le tour courant.
Pour une V2 plus robuste, ajouter :

- RPC SQL ou Edge Function pour creer le matchmaking de facon atomique;
- validation serveur des coups;
- Supabase Realtime WebSocket;
- reconnexion;
- abandon de partie;
- timer;
- classement.

## 10. Online Multiplayer Fixes

### Pourquoi les deux clients voyaient "tour de l'adversaire"

Avant le correctif, quand deux joueurs A et B cliquaient "Jouer en ligne" presque simultanement, chacun pouvait detecter l'autre en file d'attente et **creer son propre match** ou il devenait `player2`. Deux matchs etaient crees, et chacun voyait :

```
player1_id = adversaire
player2_id = moi
current_turn_id = adversaire
```

Donc les deux clients restaient sur "tour de l'adversaire".

### Le correctif Unity (V1)

`OnlineMatchService.TryFindOrCreateMatchRoutine` applique maintenant un tie-break deterministe avant de creer un match :

1. Si je n'ai pas encore de ligne dans `matchmaking_queue` (premiere iteration), je cree le match. Je deviens `player2`, l'autre joueur (qui attendait) devient `player1` et joue en premier.
2. Si ma ligne est plus ancienne que celle de l'adversaire (`created_at` inferieur), je suis arrive en premier : j'attends, c'est a l'autre de creer.
3. Si ma ligne est plus recente que celle de l'adversaire, je cree.
4. Si les deux `created_at` sont identiques au timestamp exact (rare), tie-break par `user_id` lexicographique.

Combine au fallback existant qui detecte les matchs deja actifs ou je suis participant, cela garantit qu'un seul match est cree par couple de joueurs.

### Garanties cote serveur

Aucun changement SQL n'est strictement requis si vous avez deja execute le bloc `## 9. Online Multiplayer`. Verifier toutefois :

- `matchmaking_queue` a la policy `matchmaking_update_own_or_claim_waiting` pour que le createur puisse marquer la file de l'autre comme `matched`.
- `online_matches` a `online_matches_insert_participant` qui autorise un participant a creer un match avec `current_turn_id = player1_id` et `status = 'active'`.
- `online_moves` a `online_moves_insert_current_turn` qui exige `auth.uid() = player_id` et que ce soit le tour de ce joueur.

### Si une demande d'ami semble bloquee

- Verifier que les deux profils existent dans `profiles` (`select * from profiles where username = 'xxx';`).
- L'ajout par username utilise le username **normalise** (minuscules, alphanumeriques + underscore). Le `SanitizeUsername` cote signup applique la meme normalisation, donc le lookup doit aboutir tant que l'utilisateur s'est inscrit via l'app.
- Si vous avez insere un profil manuellement dans `profiles`, mettre `username` en minuscules.

### Si une invitation de partie reste en `pending` cote sender

- Le sender poll `match_invites?from_user_id=eq.me&status=eq.accepted&match_id=not.is.null&limit=1` toutes les ~5 secondes via `FriendsView`.
- Tant que le receveur n'a pas clique `Accepter`, l'invitation reste `pending`. Cote sender on n'affiche pas de message d'erreur.
- Si vous voulez tester rapidement, ouvrir le SQL editor et faire :
  ```sql
  select * from match_invites order by created_at desc limit 5;
  ```

### Logs Unity utiles pour debugger

Le client Unity loggue maintenant :

- `[Online] Loaded match <id> p1=<...> p2=<...> turn=<...> local=<...> localMark=<...> isMyTurn=<...>` au chargement de la partie online.
- `[Online] Fresh fetch match <id> turn=<...> status=<...>` apres le refresh initial cote serveur.
- `[Online] Poll match <id> turn=<...> status=<...> winner=<...>` chaque fois que le tour ou le statut change pendant le polling.
- `[Online] Sent move #<n> ...` quand un coup est envoye.
- `[Online] Applied remote move #<n> from <userId>` quand un coup adverse est applique.
- `[Matchmaking] Start user=<id> game=<kind>` au lancement de la recherche.
- `[Matchmaking] Found opponent=<id> queue=<id>` quand un adversaire fresh est detecte.
- `[Matchmaking] Waiting queue=<id> (opponent ... should create the match)` cote tie-break.
- `[Matchmaking] Matched queue match=<id>` quand ma queue est marquee matched.
- `[Matchmaking] Created match=<id> p1=<id> p2=<id> turn=<id>` quand je cree le match.
- `[Matchmaking] Refused invalid match creation: reason=<...>` quand un garde a refuse une creation (queue plus claimable, id vide, self-match, etc.).
- `[Online] Accepted invite <id>, match=<matchId>` quand une invitation est acceptee cote receveur.
- `[Online] Accepted invite (sender side) <id>, match=<matchId>` quand le sender detecte l'acceptation.

Ouvrir `Window > General > Console` dans Unity et observer ces logs des deux cotes pour comprendre l'etat du match.

## 11. Matchmaking hasard / "match avec du vide"

### Symptomes observes

- Deux joueurs cliquent "Jouer en ligne" sans etre amis. Au lieu d'etre relies ensemble, un client passe en gameplay avec un adversaire fantome (player1_id appartient a un compte qui n'est pas en ligne) ou les deux restent bloques en "Recherche d'un joueur...".
- Apres une partie online finie, recliquer "Jouer en ligne" relance immediatement la partie deja terminee.

### Cause racine

`matchmaking_queue` est purement declaratif cote client. Quand un joueur ferme Unity sans appuyer sur Annuler, sa ligne reste `status='waiting'` indefiniment. Le client suivant lit cette ligne fantome, cree un `online_match` avec elle comme `player1`, et se retrouve seul cote `player2`.

De plus, apres un match termine, la ligne de queue restait `status='matched'` avec un `match_id` pointant vers une partie deja `finished`. Le code Unity retombait dessus a la prochaine recherche.

### Le correctif Unity (V1)

Modifications dans `OnlineMatchService.cs` :

1. **Bootstrap propre**. `StartMatchmaking` appelle d'abord `CancelAllOwnQueuesRoutine` (PATCH `status='cancelled', match_id=null` sur toutes mes lignes `waiting`/`matched` du game_kind), puis `EnsureFreshOwnQueueRoutine` qui upsert avec `status='waiting'`, `match_id=null` ET `created_at=now`. Le `created_at` frais permet au tie-break de fonctionner meme apres un cancel/re-clic.
2. **Anti-fantome**. `FetchWaitingOpponentRoutine` filtre desormais `match_id is null` ET `updated_at >= now() - 90 secondes`. Une ligne dont `updated_at` n'a pas ete rafraichie depuis 90s est ignoree.
3. **Heartbeat**. A chaque iteration du polling (toutes les 2s), Unity reupsert ma ligne `waiting` (sans toucher a `created_at`) pour rafraichir `updated_at`. Tant que mon Unity tourne, ma ligne reste fresh ; des qu'il se ferme, elle devient stale et plus aucun client ne la voit.
4. **Re-check juste avant CreateMatch**. Apres avoir trouve un adversaire, Unity re-fetch sa ligne par id et verifie : `status='waiting'`, `match_id` toujours null, `updated_at` toujours fresh. Si non, on abandonne la creation et on attend la prochaine iteration.
5. **Gardes stricts dans `CreateMatchRoutine`**. Refus si `game_kind` autre que Quixo/Qomet, si l'un des ids est vide, ou si `player1_id == player2_id`. Le log emis est `[Matchmaking] Refused invalid match creation: reason=...`.
6. **Ne pas retomber sur un match termine**. `FetchActiveMatchForLocalRoutine` filtre desormais `updated_at >= now() - 5 minutes`. Si ma queue est `matched` vers un match `finished`, je le detecte et reset ma queue a `waiting`.

### Faut-il executer du SQL ?

**Non**, le correctif est uniquement cote Unity. Les tables et policies du bloc `## 9. Online Multiplayer` suffisent.

### Optionnel : RPC SQL matchmake_user

Si vous voulez une atomicite serveur (utile si plus de 2 joueurs cherchent simultanement), executer cette RPC. Elle remplace toute la logique cote Unity par un seul appel `POST /rest/v1/rpc/matchmake_user`. Le client Unity n'utilise PAS encore cette RPC : elle est documentee ici pour une eventuelle V2.

```sql
create or replace function public.matchmake_user(p_game_kind text)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  v_me uuid := auth.uid();
  v_opponent matchmaking_queue%rowtype;
  v_match_id uuid;
begin
  if v_me is null then
    raise exception 'not authenticated';
  end if;

  if p_game_kind not in ('Quixo', 'Qomet') then
    raise exception 'invalid game_kind: %', p_game_kind;
  end if;

  -- 1. Reset ma queue : status=waiting, match_id=null, created_at=now.
  insert into matchmaking_queue (user_id, game_kind, status, match_id, created_at, updated_at)
  values (v_me, p_game_kind, 'waiting', null, now(), now())
  on conflict (user_id, game_kind) do update
    set status = 'waiting',
        match_id = null,
        created_at = now(),
        updated_at = now();

  -- 2. Chercher un adversaire fresh, atomiquement.
  select * into v_opponent
  from matchmaking_queue
  where game_kind = p_game_kind
    and status = 'waiting'
    and user_id <> v_me
    and match_id is null
    and updated_at >= now() - interval '90 seconds'
  order by created_at asc
  limit 1
  for update skip locked;

  if not found then
    return null; -- on reste en waiting, le client polling retentera.
  end if;

  -- 3. Creer le match.
  insert into online_matches (game_kind, player1_id, player2_id, current_turn_id, status)
  values (p_game_kind, v_opponent.user_id, v_me, v_opponent.user_id, 'active')
  returning id into v_match_id;

  -- 4. Marquer les deux queues comme matched.
  update matchmaking_queue
    set status = 'matched', match_id = v_match_id, updated_at = now()
    where id = v_opponent.id;

  update matchmaking_queue
    set status = 'matched', match_id = v_match_id, updated_at = now()
    where user_id = v_me and game_kind = p_game_kind;

  return v_match_id;
end;
$$;

grant execute on function public.matchmake_user(text) to authenticated;
```

Avantages : verrou `for update skip locked` garantit qu'aucun autre RPC ne peut prendre le meme adversaire. Aucun risque de double match.

Si vous activez cette RPC, ouvrir un ticket Unity pour remplacer `TryFindOrCreateMatchRoutine` par un seul `POST /rest/v1/rpc/matchmake_user` avec body `{ "p_game_kind": "Quixo" }`. **Tant que vous n'avez pas migre Unity, ne lancez PAS cette RPC car elle n'est pas appelee** : juste la creer ne casse rien, mais elle reste dormante.
