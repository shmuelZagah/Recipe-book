# Recipe Book

A cross-platform recipe management app built with .NET MAUI, combining local-first SQLite storage with cloud sync, multi-user folder sharing, and an automatic shopping-list generator that aggregates ingredients across a weekly meal plan.

---

## Highlights

- **Local-first, cloud-synced architecture** — every write goes to a local SQLite database first, so the UI updates instantly regardless of connectivity. Firestore and Cloudinary sync happen in the background when a network is available.
- **Offline operation queue** — actions that can't reach the cloud immediately (like deletions) are persisted locally as pending operations (`PendingCloudDeletion`, `PendingSharedFolderDeletion`, `PendingSharedListDeletion`) and automatically flushed when `Connectivity.Current.NetworkAccess` reports the device back online — no lost actions, no manual retry.
- **Shareable recipe folders via expiring links** — an entire folder tree (subfolders + recipes) is serialized into a single JSON document and pushed to Firestore as a time-limited share (15-day expiry), letting one user send a whole recipe collection to another without either needing a live shared database.
- **Automatic shopping list generation** — `ShoppingListBuilderService` aggregates ingredients across a date range of scheduled meals, merges them with an optional reusable template list, and combines quantities by normalized name + unit — including a keyword-based unit/ingredient conversion table for matching variant ingredient names.
- **Weekly meal planning** — a calendar view groups scheduled meals by day and meal type (breakfast/lunch/dinner), feeding directly into the shopping list builder.
- **Cloud image hosting** — recipe photos are uploaded to Cloudinary rather than stored as blobs in Firestore or on-device, keeping the database lightweight.
- **MVVM with source-generated bindings** — built on `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) with `WeakReferenceMessenger` for decoupled communication between view models.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET MAUI (.NET 8) |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Local storage | SQLite (`sqlite-net`) |
| Cloud data | Firebase Firestore |
| Auth | Firebase Authentication |
| Image hosting | Cloudinary |
| Target platform | Android (project currently configured for Android; MAUI's platform abstraction means iOS/Windows/MacCatalyst targets are structurally supported) |

---

## Architecture

```
Views (XAML)
   ↓ bound to
ViewModels (CommunityToolkit.Mvvm, ObservableObject)
   ↓ call into
Models/Services
   ├── RecipesDatabase        → local SQLite (source of truth)
   ├── FirestoreService        → cloud document sync + sharing
   ├── FirebaseAuthService     → user auth
   └── Shopping/
        ├── ShoppingListBuilderService  → aggregates ingredients from schedule
        └── ShoppingListActionService

Models/Cloud   → Firestore document shapes + pending-sync queue
Models/Recipes → core recipe/ingredient/folder entities
Models/Shopping → shopping list entities
```

Local SQLite is always the immediate source of truth for the UI; Firestore/Cloudinary act as a sync and sharing layer behind it, with connectivity changes driving when queued operations get pushed.

---

## Getting Started

### Prerequisites
- Visual Studio 2022 (17.8+) with the .NET MAUI workload
- .NET 8 SDK
- Android SDK / emulator (or a physical device) for running the Android target
- A Firebase project (Firestore + Authentication enabled) and a Cloudinary account

### Configuration
This project depends on a `Secrets` class providing:
- `CloudinaryCloudName`, `CloudinaryApiKey`, `CloudinaryApiSecret`
- Firebase configuration (`google-services.json` for Android)

These are not included in the repository and must be supplied locally before the app will build/run against live services.

### Build & Run
1. Clone the repository.
2. Open `Recipe_book.sln` in Visual Studio.
3. Restore NuGet packages.
4. Add your Firebase/Cloudinary credentials as described above.
5. Set the Android target and run on an emulator or device.

---

## Project Structure

```
Models/
  ├── Recipes/        # Recipe, Ingredient, Folder entities
  ├── Shopping/        # Shopping list entities
  ├── Cloud/            # Firestore document shapes + pending-sync queue models
  ├── Organization/     # folder/library organization
  └── Services/         # RecipesDatabase (SQLite), FirestoreService, FirebaseAuthService, Shopping services
ViewModels/           # one per page — MainViewModel, LibraryViewModel, WeeklyScheduleViewModel, ShoppingListViewModel, RecipeEditorViewModel, etc.
Views/
  ├── Pages/            # top-level pages
  ├── SubPages/          # modals/detail pages
  └── Items/              # reusable list-item templates
Helpers/              # converters, behaviors, text helpers
Platforms/            # platform-specific entry points (Android, iOS, Windows, MacCatalyst, Tizen)
```

---

## Status

Actively developed; Android is the current build target. The data layer (SQLite + Firestore + Cloudinary) and core flows — recipe library, weekly schedule, shopping list generation, and folder sharing — are implemented.

---

## License

No license file is currently included — all rights reserved by default. Add a license (MIT, Apache 2.0, etc.) if you want others to reuse this code.
