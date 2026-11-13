# Gloub Village

Un village de **Gloubs**, petites créatures végétales, qui vivent en autonomie. Chaque Gloub gère ses besoins (eau, nourriture, sommeil, amitié) grâce à une IA **GOAP** (Goal-Oriented Action Planning) et a une personnalité de type **MBTI** qui change ses choix.

- 8 Gloubs : Glim (ENTJ), Nuage (INFP), Pépin (ISTP), Lune (ENFJ), Mochi (INTP), Kiki (ISFP), Bleuet (INFJ), Pompom (ESTJ).
- 4 traits de 0 à 100 : Sociabilité (introverti ↔ extraverti), Imagination (pragmatique ↔ imaginatif), Rationalité (empathique ↔ logique), Organisation (spontané ↔ organisé).
- Les besoins baissent avec le temps. Le planificateur A* choisit une suite d'actions (aller au puits, cueillir un fruit, dormir, discuter avec un ami...) et l'agent replanifie quand la situation change. Les amitiés évoluent au fil des conversations.

## Version

- Unity **6000.3.28f1**, **URP 17.3.0**, AI Navigation 2.0.16 (NavMesh), Input System 1.20.1.

## Ouvrir / lancer

1. Ouvrir le dossier avec Unity Hub (Unity 6000.3.28f1).
2. Ouvrir `Assets/Scenes/GloubVillage.unity` puis Play.
3. Pour reconstruire le village : menu **Gloub › Build Village Scene** (ou **Gloub › Build Village and Simulate**).

`Assets/Scenes/GloubShowcase.unity` présente le modèle du Gloub seul.

## Scripts principaux

- `Assets/Gloub/AI/GOAP/` : cœur GOAP (`WorldState`, `GoapAction`, `GoapGoal`, `GoapPlanner` en A*, `GoapAgent`)
- `Assets/Gloub/AI/` : `GloubNeeds` (besoins), `GloubPersonality` (traits MBTI), `PersonalityActions` et `VillageActions` (actions), `FriendshipSystem`, `GloubVillager`, `GloubMover` / `GloubLocomotion` (déplacement NavMesh), `GloubAgentUI` (étiquettes)
- `Assets/Gloub/Village/` : lieux du village (`WaterSource`, `FruitBush`, `HomeBed`, `SocialSpot`), `VillageController`, outils de capture vidéo
- `Assets/Gloub/Scripts/Editor/GloubBuilder.cs` : génère le modèle du Gloub (maillages, matériaux, prefab)
