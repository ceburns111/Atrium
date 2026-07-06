# Task 10 Report: Delete Custom Design Primitives

## Pre-Delete Git-Grep Proof (per primitive)

All greps run via `git grep` on the committed tree (tracked files only).  
Where applicable, `-- "*.razor" "*.cs"` used to exclude `obj/bin`.

| Target | Pattern | Hits outside deleted file |
|--------|---------|--------------------------|
| Button.razor | `<Button` in *.razor | 0 |
| Field.razor | `<Field` in *.razor | 0 |
| Dialog.razor | `<Dialog[^C]` (excluding MudDialog sub-tags) | 0 |
| Menu.razor | `<Menu ` in *.razor | 0 |
| Badge.razor | `<Badge` (non-MudBadge) | 0 |
| Notice.razor | `<Notice` | **3 hits — required conversion** (Error, Forbidden, NotFound portal pages) |
| PageHeader.razor | `<PageHeader` | **1 hit — required conversion** (Home.razor) |
| atrium.css | `_content/Atrium.Design/css/atrium.css` | 0 (already removed from App.razor in Task 2) |
| tokens.css | `_content/Atrium.Design/css/tokens.css` | 0 (already removed from App.razor in Task 2) |
| theme.js | `theme.js` | 0 (file did not exist — deleted in Task 4) |
| dialog.js | `dialog.js` | 0 |

### Blockers Found and Fixed

`<Notice>` was still used in 3 portal pages; `<PageHeader>` in 1 portal page.
These pages were not converted in earlier tasks. Converted before deletion:

- `src/Atrium.Portal/Components/Pages/Error.razor` — replaced `<Notice>` + `class="notice__body"` + `<a class="btn btn--secondary">` with `<MudContainer>`, `<MudStack>`, `<MudText>`, `<MudButton>`
- `src/Atrium.Portal/Components/Pages/Forbidden.razor` — same pattern
- `src/Atrium.Portal/Components/Pages/NotFound.razor` — same pattern
- `src/Atrium.Portal/Components/Pages/Home.razor` — replaced `<PageHeader>` (eyebrow/title/desc) with `<MudStack>` + `<MudText>` header pattern; replaced custom CSS class-based module-grid/module-card with `<MudGrid>` / `<MudItem>` / `<MudCard>`; replaced `section-label` div with `<MudText Typo.overline>`; replaced `empty` class with `<MudText Class="mud-text-secondary">`.

`MenuTests.cs` in Atrium.UnitTests also referenced the deleted `Menu` type — discovered at first build attempt after deletion, removed with `git rm`.

## Files Actually Deleted

```
git rm src/Atrium.Design/Components/Badge.razor
git rm src/Atrium.Design/Components/Button.razor
git rm src/Atrium.Design/Components/Dialog.razor
git rm src/Atrium.Design/Components/Dialog.razor.css
git rm src/Atrium.Design/Components/Field.razor
git rm src/Atrium.Design/Components/Menu.razor
git rm src/Atrium.Design/Components/Notice.razor
git rm src/Atrium.Design/Components/Notice.razor.css
git rm src/Atrium.Design/Components/PageHeader.razor
git rm src/Atrium.Design/wwwroot/css/atrium.css
git rm src/Atrium.Design/wwwroot/css/tokens.css
git rm src/Atrium.Design/wwwroot/js/dialog.js
git rm tests/Atrium.UnitTests/MenuTests.cs   ← bunit test of deleted Menu primitive
```

Also modified (not deleted):
- `src/Atrium.Design/AtriumTheme.cs` — FontMono field removed (see below)
- 4 portal pages — converted to MudBlazor as above

**Files that already did not exist (deleted in Tasks 4):**
- `ThemeToggle.razor`, `ToastHost.razor`, `Toasts.cs`, `theme.js` — confirmed absent before this task.

## FontMono Outcome

`FontMono` was a `private static readonly string[]` field in `AtriumTheme.cs` declared on line 30.  
`git grep "FontMono"` returned only that declaration — it was **never assigned to any Typography role** and referenced nowhere outside its own declaration.  
**Deleted** per YAGNI instruction.

## Post-Delete Grep-Clean Confirmation

After all deletions:

```
git grep "<Button|<Field|<Dialog|<Menu|<Badge|<Notice|<PageHeader" -- *.razor *.cs → (none)
git grep "atrium.css|tokens.css|theme.js|dialog.js|_content/Atrium.Design/css|_content/Atrium.Design/js" → (none)
git grep "FontMono" -- *.cs *.razor → (none)
```

`wwwroot/css/` and `wwwroot/js/` directories were emptied and removed by git.

## Gate Output

```
dotnet csharpier format .
→ Formatted 76 files in 104ms.

dotnet build Atrium.slnx -v q
→ Build succeeded. 0 Warning(s) 0 Error(s)  Time Elapsed 00:00:02.51
```

## Test Output

```
dotnet test tests/Atrium.UnitTests
→ Passed!  total: 64  failed: 0  succeeded: 64

dotnet test tests/Atrium.IntegrationTests
→ Passed!  total: 12  failed: 0  succeeded: 12
```

**Integration lane ran** — Docker was available; all 12 integration tests passed.

Unit count is 64 (was 68 before this task; 4 deleted `MenuTests` methods account for the difference).
