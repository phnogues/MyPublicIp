# Privacy policy – My Public IP Address

*Last updated: October 19, 2026*

My Public IP Address shows the public IP address of your internet connection in the Windows notification area, where it is located, whether a VPN is on, and the quality of the connection.

## What the app sends over the internet

- **To find your public IP address**, a simple HTTPS request to one of these public services:
  `api.ipify.org` / `api6.ipify.org` (ipify), `ipv4.icanhazip.com` / `ipv6.icanhazip.com` (icanhazip),
  `checkip.amazonaws.com` (Amazon Web Services). They only reply with the address the request came from.
- **To show where this address is located and who provides it**, the public address is sent to
  `ipinfo.io` (or `ipapi.co` if it does not answer), only when the address changes. These services
  reply with the country, region, city and internet provider registered for that address.
- **To measure the latency**, a ping (or, if pings are blocked, the opening of a connection that is
  closed right away) to Cloudflare's public server `1.1.1.1`. Nothing is sent with it.

Like any web server, these services see the public IP address the request comes from. They do not
receive any other information from the app.

## What the app reads on your device

The network adapter used to reach the internet (local address, gateway, DNS servers, MAC address,
network name and signal strength) and the presence of a VPN adapter. These details are only shown
in the app; they are never sent anywhere.

## What stays on your device

- The list of your recent public addresses (up to 20, with the date they were seen, their country
  and the VPN connected at that time), shown in the app.
- A few settings: start with Windows, notifications, keyboard shortcut, first-run setup done, tip closed.

This data never leaves your device. Uninstalling the app removes it.

## What the app does not do

- It does not collect or send any personal data to the developer.
- It does not use analytics, advertising or tracking.
- It does not ask for an account.
- It does not use the location of your device: the location shown is the one registered for your public IP address.

## Contact

For any question, open an issue at https://github.com/phnogues/MyPublicIp/issues.

---

# Politique de confidentialité – My Public IP Address

*Dernière mise à jour : 19 octobre 2026*

My Public IP Address affiche l'adresse IP publique de votre connexion Internet dans la zone de notification de Windows, l'endroit où elle est localisée, la présence d'un VPN et la qualité de la connexion.

**Ce que l'application envoie sur Internet :**

- **Pour trouver votre adresse IP publique**, une simple requête HTTPS à l'un des services publics listés ci-dessus (ipify, icanhazip, Amazon Web Services). Ils répondent uniquement avec l'adresse d'où vient la requête.
- **Pour afficher la localisation et le fournisseur de cette adresse**, l'adresse publique est envoyée à `ipinfo.io` (ou `ipapi.co` s'il ne répond pas), uniquement quand elle change. Ces services répondent avec le pays, la région, la ville et le fournisseur d'accès enregistrés pour cette adresse.
- **Pour mesurer la latence**, un ping (ou, si les pings sont bloqués, l'ouverture d'une connexion aussitôt refermée) vers le serveur public de Cloudflare `1.1.1.1`. Rien n'est envoyé avec.

Comme tout serveur web, ces services voient l'adresse IP publique d'où vient la requête. Ils ne reçoivent aucune autre information.

**Ce que l'application lit sur votre appareil :** la carte réseau utilisée pour accéder à Internet (adresse locale, passerelle, serveurs DNS, adresse MAC, nom du réseau, force du signal) et la présence d'une carte VPN. Ces informations sont seulement affichées dans l'application et ne sont jamais envoyées.

**Ce qui reste sur votre appareil :** la liste de vos adresses publiques récentes (20 au maximum, avec leur date, leur pays et le VPN connecté à ce moment-là) et quelques réglages (démarrage avec Windows, notifications, raccourci clavier, premier lancement, astuce fermée). Ces données ne quittent jamais votre appareil et sont supprimées avec l'application.

L'application ne collecte et n'envoie aucune donnée personnelle au développeur. Elle n'utilise ni statistiques, ni publicité, ni suivi, et ne demande aucun compte. Elle n'utilise pas la position de votre appareil : la localisation affichée est celle enregistrée pour votre adresse IP publique.

Contact : https://github.com/phnogues/MyPublicIp/issues
