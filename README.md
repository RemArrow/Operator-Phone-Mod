# OperatorPhone

An in-game smartphone for [OPERATOR](https://store.steampowered.com/app/2216130/OPERATOR/).
Players get a persistent phone number and can text each other — across servers, in
menus, or mid-op.

> **Status: early development.** Identity and the app shell work. Messaging is
> implemented but lightly tested. Not yet suitable for general use.

<!-- TODO: screenshot of the phone with a conversation open -->

---

## What it does

- **Persistent phone numbers.** Link your Steam account once, get a number like
  `201-9219` that survives reinstalls.
- **Texting.** 1:1 conversations with local history, delivered over Photon Chat.
- **Contacts.** Save numbers with nicknames.
- **Presence.** See which contacts are online. *(planned)*
- **Media and links.** Inline images with server-side previews. *(planned)*

## Why it doesn't touch Mirror

OPERATOR uses [Mirror](https://mirror-networking.com/) for netcode, and Mirror's
compile-time weaving makes adding new `NetworkBehaviour` types to a shipped IL2CPP game
impractical for modders.

OperatorPhone sidesteps this entirely by running its own transport in parallel. It never
registers a prefab, never spawns a networked object, and never touches
`NetworkServer`. Consequences worth knowing:

- Messaging works **outside matches** — main menu, lobby, or between sessions.
- Two players on **different servers** can still text each other.
- The mod **cannot desync or crash** the game's networking, because it isn't part of it.
- Non-modded players are invisible to the phone. The fiction absorbs this fine: not
  everyone has your number.

## Install

1. Install [MelonLoader](https://melonwiki.xyz/) (v0.6+) into OPERATOR.
2. Download `OperatorPhone.dll` from [Releases](../../releases).
3. Drop it in `OPERATOR/Mods/`.
4. Launch, press **F1**, click **Link Steam Account**, finish the login in your browser.

Single DLL — dependencies are embedded. Nothing goes in `UserLibs`.

### Keys

| Key | Action |
|---|---|
| `F1` | Open / close the phone |
| `F10` | Input system probe *(dev)* |
| `F11` | Type dumper *(dev)* |

Rebindable in `UserData/MelonPreferences.cfg`.

## Privacy

- Your SteamID is **never stored**. The server keeps `HMAC(steamid, secret)` — a hash it
  cannot reverse — so a database breach yields no number-to-Steam-account map.
- Numbers are one-directional: given a number you can send a message, but you cannot
  resolve a number back to a Steam account.
- Messages are **not end-to-end encrypted.** They pass through Photon in transit. Treat
  the phone like an unencrypted chat, not like Signal.
- Message history is stored locally under `UserData/OperatorPhone/`.

## Architecture

```
┌──────────────────────────────┐
│  OPERATOR (Unity 6, IL2CPP)  │
│  ┌────────────────────────┐  │
│  │ OperatorPhone (Melon)  │  │
│  │  ├ PhoneShell / AppHost│  │
│  │  ├ ChatService         │──┼──── Photon Chat ──── other players
│  │  ├ IdentityService     │──┼──── Worker ───┐
│  │  └ MessageStore (disk) │  │               │
│  └────────────────────────┘  │        Cloudflare D1 + KV
└──────────────────────────────┘
```

**Auth is Steam OpenID**, not a Steam auth session ticket. Validating a ticket requires
a *publisher* Web API key scoped to the app, which only the game's developer can issue —
so that route is permanently closed to third-party mods. OpenID needs no App ID and no
API key: the player authenticates with Valve directly in a browser, and the Worker
verifies the assertion by echoing it back to Steam.

**Number assignment is atomic.** The line number derives from
`HMAC(steamid, secret) mod 10000`, then `INSERT` and let a `UNIQUE` constraint arbitrate,
linear-probing on collision. Uniqueness is enforced by the database rather than a
read-then-write check, which would race two simultaneous logins onto the same number.

Area codes encode join cohort — `201` for the first block, `202` next, rolling over at
50% occupancy. Retired numbers are never reissued: a banned player's contacts would
otherwise start messaging whoever inherited it.

## Building

Requires the .NET 6 SDK and a MelonLoader-patched copy of OPERATOR.

```bash
git clone https://github.com/<you>/OperatorPhone
cd OperatorPhone
```

**Dependencies not in this repo** (licensing):

1. [Photon .NET SDK v5](https://www.photonengine.com/sdks) — copy
   `PhotonLibs/Release/netstandard2.0/PhotonClient.dll` to `lib/`, and the `.cs` files
   from `PhotonChatApi/` to `lib/PhotonChatApi/`.
2. Run OPERATOR once with MelonLoader so it generates `MelonLoader/Il2CppAssemblies/`.

Then:

```bash
dotnet build -p:GameDir="C:\Path\To\OPERATOR"
```

Builds straight into `Mods/`.

Fill in `Core/ServiceConfig.cs` with your own Photon Chat App ID and Worker URL if
you're running your own backend.

### Server

The identity worker lives in [`worker/`](worker/) — Cloudflare Workers + D1 + KV, all
within free tiers. See [`worker/README.md`](worker/README.md) for deployment.

You do **not** need to run a server to use the mod, and there is no server on your PC.
Everything is serverless and hosted.

## Roadmap

- [x] **M0** — UI harness, input capture
- [x] **M1** — Steam OpenID identity, number assignment
- [ ] **M2** — Photon Chat, texting *(in progress)*
- [ ] **M3** — Offline delivery, message history sync
- [ ] **M4** — Inline images
- [ ] **M5** — Link previews
- [ ] **M6** — Spotify remote, curated browser
- [ ] **M7** — Moderation, rate limits, hardening

## Contributing

Issues and PRs welcome. Worth reading first:

- Input handling must never sit downstream of a network call. If the phone can't open
  because chat failed, that's a bug in ordering, not in chat.
- Envelope changes need a `v` bump and graceful degradation — an older client must
  ignore an unknown message type, not throw.
- Anything rendering remote content is a moderation surface. Assume hostile input.

## Not affiliated

Not affiliated with or endorsed by Vector Interactive. OPERATOR is their work; this is
an unofficial mod.

## License

MIT — see [LICENSE](LICENSE).

Photon and Steamworks.NET are covered by their own licenses and are not redistributed
in this repository.
