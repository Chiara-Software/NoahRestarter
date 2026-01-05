# Noah Restarter

Noah Restarter est un petit outil Windows qui permet de relancer proprement les services Noah dans le bon ordre, afin de résoudre rapidement certains blocages (application figée, services instables, etc.).
Il est compatible avec les versions de Noah 4 jusqu'à la 4.117.0 (denière version en date du 05/01/2026) - Il fonctionne également avec le client Noah ES.
Contrèrement au redémarrage manuel des services Noah, Noah Restarter permet à un utilisateur Windows standard (non administrateur) de stopper puis relancer les services Noah.

Son usage est recommandé en première intention lors de l'erreur 50138 (https://www.himsa.com/support/noah-4-knowledge-base/noah-4-troubleshooting/error-50138-when-starting-noah-4/)
Il automatise la Solution 1 (la plus fréquente) - si malgré tout vous continuez de rencontrer cette erreur, nous vous invitons à suivre les autres solutions disponible sur la page de l'HIMSA.

## Fonctionnement

L’application déclenche une tâche planifiée Windows exécutée en **SYSTEM** (LocalSystem), laquelle lance un script PowerShell. Le script applique la séquence suivante :

1. Si Noah est lancé (process `noah4.exe`), tentative de fermeture puis arrêt forcé si nécessaire.
2. Arrêt du service **NoahClient**
3. Arrêt du service **NoahClient**
4. Arrêt du service **NoahServerUtil**
5. Démarrage du service **NoahServerUtil**
6. Démarrage du service **NoahServer**
7. Démarrage du service **NoahClient**
8. Relance de Noah côté session utilisateur, puis fermeture automatique de l’interface.

L’interface affiche l’avancement (initialisation / arrêt / redémarrage / terminé).

## Pré-requis

- Windows 10 / Windows 11 (et Windows Server selon votre environnement)
- Services installés :
  - `NoahServerUtil` (optionnel présent à partir de Noah 4.117.0)
  - `NoahServer`
  - `NoahClient`
- Windows PowerShell 5.1 (présent par défaut sur Windows)

## Installation (MSI) : [ Télécharger le MSI ](https://github.com/Chiara-Softwares/NoahRestarter/blob/main/Advanced%20Installer/Setup%20Files/Noah%20Restarter-latest.msi?raw=1)

L’installateur déploie :
- Application : `C:\Program Files\Noah Restarter\`
- Script : `C:\ProgramData\Chiara Software\Noah Restarter\Restart-Noah.ps1`
- Tâche planifiée : `Restart Noah Services` (exécutée en SYSTEM)
- Modification des droits de la tâche planifiée pour qu'elle puisse être exécutée par un utilisateur non Administrateur sur la machine

## Utilisation

- Lancez Noah Restarter
- Patientez pendant l’exécution (la fenêtre se ferme automatiquement en fin de procédure)
- Noah est relancé automatiquement

## Développement

- UI : C# WinForms (.NET Framework 4.8)
- Déclenchement : schtasks.exe
- Exécution : tâche planifiée (SYSTEM) → PowerShell Restart-Noah.ps1

## Auteur
- Éditeur : Chiara Software
- Auteur : Maxime Barruet

## Licence
Ce projet est distribué sous licence GNU General Public License v3.0 (GPLv3).
Vous pouvez redistribuer et/ou modifier ce programme selon les termes de la GNU GPL v3 telle que publiée par la Free Software Foundation.
Ce programme est distribué dans l’espoir qu’il soit utile, mais SANS AUCUNE GARANTIE ; sans même la garantie implicite de QUALITÉ MARCHANDE ou d’ADÉQUATION À UN BUT PARTICULIER.
Texte complet de la licence : https://www.gnu.org/licenses/gpl-3.0.txt
