# BattleShip

Projet ASP.NET/Blazor pour jouer à la bataille navale contre l’IA ou en multijoueur.

Remy LOURON
Killian PAVY
Clément TRENS

## Prérequis

- .NET 8 SDK
- Navigateur récent (Chrome, Edge, Firefox…)

## Installation

Depuis la racine du dépôt :

```powershell
dotnet restore
```

## Lancer le backend (API)

1. Ouvrir un terminal.
2. Exécuter :

    ```powershell
    cd BattleShip.API
    dotnet run
    ```

L’API écoute par défaut sur `https://localhost:7096` et `http://localhost:5086`.

## Lancer le frontend (Blazor WebAssembly)

1. Ouvrir un second terminal.
2. Exécuter :

    ```powershell
    cd BattleShip.App
    dotnet run
    ```

Ouvrir ensuite l’URL indiquée dans la console (souvent `https://localhost:7214`).

## Renouveler / approuver le certificat de développement

Si le navigateur bloque les appels HTTPS (erreur `ERR_CERT_AUTHORITY_INVALID`), régénérer et approuver le certificat ASP.NET Core :

```powershell
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

Accepter la boîte de dialogue Windows pour approuver le certificat, puis relancer `dotnet run` côté API et client.

Si ça ne fonctionne pas, essayez d'aller sur https://localhost:7096 dans le navigateur, puis approuvez le certificat via l'interface du navigateur.


