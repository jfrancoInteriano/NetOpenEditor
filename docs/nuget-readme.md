# NetOpenEditor

**Inline line editor for header-detail forms in ASP.NET Core MVC (.NET 10).**

[![NuGet](https://img.shields.io/nuget/v/NetOpenEditor?style=flat-square&logo=nuget&label=NuGet)](https://www.nuget.org/packages/NetOpenEditor)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-22c55e?style=flat-square)](https://github.com/jfrancoInteriano/NetOpenEditor/blob/master/LICENSE)

Declare the columns once in C#. An Alpine.js runtime edits cell by cell without rebuilding the DOM, and the
header form posts `Lines[i].Field` exactly as hand-written inputs would: **zero changes to controllers or services**.

- Permanent empty row at the end (spreadsheet style); no "Add" button.
- Editors: text, integer, decimal, date, select, keyboard-driven remote lookup, toggle, read-only, computed, hidden.
- Keyboard: Tab, Enter (next row), Up/Down, Escape (revert), Ctrl+Delete (remove row).
- Paste multi-cell ranges from Excel; lookup columns resolve pasted values.
- `ModelState` errors per cell and per row; round-trip after a failed POST.
- Per-view hooks for domain rules, footer totals, DOM events for side panels.
- One package, embedded JS/CSS. **Alpine.js 3 is provided by the host** (not bundled).

Full documentation (Spanish), a runnable sample and the tests live on GitHub:
<https://github.com/jfrancoInteriano/NetOpenEditor>

## Install

```bash
dotnet add package NetOpenEditor
```

## Minimal usage

**Program.cs**

```csharp
builder.Services.AddNetOpenEditor()
    .AddEditor<JournalLine>("journal-lines", e => e
        .Column(l => l.AccountId, c => c.Header("Account").Required()
            .Lookup("/accounts/lookup", lk => lk
                .ValueField("accountId").LabelField("display").Display("code", "name")))
        .Column(l => l.Description, c => c.Header("Description"))
        .Column(l => l.DebitAmount,  c => c.Header("Debit").Decimal(2).Total())
        .Column(l => l.CreditAmount, c => c.Header("Credit").Decimal(2).Total())
        .MinRows(1));

var app = builder.Build();
app.MapNetOpenEditor();   // GET /_noe/netopeneditor.js and /_noe/netopeneditor.css
```

**_Layout.cshtml** (in `<head>`; load Alpine with `defer` afterwards):

```cshtml
@inject NetOpenEditor.Options.NetOpenEditorAssetOptions EditorAssets
@Html.Raw(NetOpenEditor.Rendering.EditorAssetTags.Head(EditorAssets))
<script src="~/lib/alpinejs/cdn.min.js" defer></script>
```

**_ViewImports.cshtml**

```cshtml
@addTagHelper *, NetOpenEditor
```

**Edit.cshtml**

```cshtml
<form method="post">
    <!-- header fields -->
    <netopen-editor editor-id="journal-lines" rows="Model.Lines" name-prefix="Lines" />
    <button type="submit">Save</button>
</form>
<script>
document.addEventListener('alpine:init', () => {
  NetOpenEditor.configure('journal-lines', {
    onCellChange(row, field, editor) {
      if (field === 'DebitAmount'  && editor.num(row.DebitAmount)  > 0) row.CreditAmount = 0;
      if (field === 'CreditAmount' && editor.num(row.CreditAmount) > 0) row.DebitAmount  = 0;
    }
  });
});
</script>
```

The controller receives `List<JournalLine> Lines` bound by index, as usual.

## License

MIT. Source: <https://github.com/jfrancoInteriano/NetOpenEditor>
