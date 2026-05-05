# V2 LAN (Host/Client)

Cette V1 n'active pas le reseau, mais cette base prepare la V2:

- Mode Host (ta machine)
- Mode Client (IP du host)
- Autorite serveur sur les regles et validation des coups
- Synchronisation d'etat apres chaque action validee

## Recommandation technique

- Unity Netcode for GameObjects + Unity Transport
- Messages:
  - `JoinRequest`
  - `MoveRequest`
  - `StateSnapshot`
  - `GameOverEvent`

## Etapes V2

1. Ajouter ecran Host/Join (IP + port).
2. Synchroniser `BoardState` via snapshots.
3. Verrouiller input client hors tour.
4. Ajouter reprise en cas de desync simple.
