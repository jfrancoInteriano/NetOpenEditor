# NetOpenEditor

Editor inline de líneas para forms **header-detalle** en ASP.NET Core MVC (.NET 10). Las columnas se
declaran en C# una sola vez, el runtime Alpine.js edita celda por celda sin reconstruir el DOM, y el
form del encabezado postea `Lines[i].Campo` como siempre: **cero cambios en controllers ni services**.

- Fila vacía permanente al final (estilo hoja de cálculo); sin botón "Agregar".
- Editores: texto, entero, decimal, fecha, select, lookup remoto con teclado, toggle, solo lectura, calculada, oculta.
- Teclado: Tab, Enter (fila siguiente), ↑/↓, Escape (revierte), Ctrl+Supr (elimina).
- Errores del `ModelState` por celda y por fila; round-trip tras un POST fallido.
- Hooks por vista para reglas de dominio; totales en pie; eventos DOM para paneles laterales.
- Un solo paquete NuGet. **Alpine.js 3 lo provee el host** (no se embebe).

[![NuGet](https://img.shields.io/nuget/v/NetOpenEditor?style=flat-square&logo=nuget&label=NuGet)](https://www.nuget.org/packages/NetOpenEditor)

## Instalación

```bash
dotnet add package NetOpenEditor
```

### Feed local (desarrollo)

```bash
./tools/pack.sh dev.1        # → artifacts/NetOpenEditor.<VersionPrefix>-dev.1.nupkg
```

`nuget.config` del consumidor:
```xml
<packageSources>
  <add key="netopeneditor-local" value="D:\NetOpenEditor\artifacts" />
</packageSources>
```
```bash
dotnet add package NetOpenEditor --version 1.0.0-dev.1
```
Usa un sufijo `-dev.N` distinto por iteración: NuGet cachea id + versión.

## Versionado y publicación

Igual que NetOpenGrid: `<VersionPrefix>` en `Directory.Build.props` es la versión base y
`.github/workflows/publish.yml` publica en nuget.org en cada push a `master`:

- Si el último tag `vX.Y.Z` comparte `MAJOR.MINOR` con `VersionPrefix`, sube el PATCH (`v1.0.3` → `1.0.4`).
- Si cambias `MAJOR` o `MINOR` en `VersionPrefix`, el siguiente publish arranca exactamente en esa versión.
- Tras publicar crea el tag `vX.Y.Z` y un GitHub Release con el `.nupkg` y `.snupkg`.
- Los PR solo compilan, prueban y empaquetan; no publican.

Requiere el secret `NUGET_API_KEY` y el environment `nuget` en el repositorio.

## Uso mínimo

**Program.cs**
```csharp
builder.Services.AddNetOpenEditor(configureLocalization: loc => loc.UseCulture("es"))
    .AddEditor<JournalLine>("journal-lines", e => e
        .Column(l => l.AccountId, c => c.Header("Cuenta").Required()
            .Lookup("/accounting/chart-of-accounts/lookup", lk => lk
                .ValueField("accountId").LabelField("display").Display("code", "name")
                .Label(l => l.AccountCode is null ? null : $"{l.AccountCode} - {l.AccountName}")
                .Companion(l => l.AccountCode, "code")
                .Companion(l => l.AccountName, "name")))
        .Column(l => l.Description, c => c.Header("Descripción"))
        .Column(l => l.DebitAmount, c => c.Header("Débito").Decimal(2).Total())
        .Column(l => l.CreditAmount, c => c.Header("Crédito").Decimal(2).Total())
        .MinRows(1));

var app = builder.Build();
app.MapNetOpenEditor();      // GET /_noe/netopeneditor.js · /_noe/netopeneditor.css (anónimos, ?v= inmutable)
```

**_Layout.cshtml** — en `<head>`; Alpine se carga `defer` al final del body, **después**:
```cshtml
@inject NetOpenEditor.Options.NetOpenEditorAssetOptions EditorAssets
@Html.Raw(NetOpenEditor.Rendering.EditorAssetTags.Head(EditorAssets))
...
<script src="~/lib/alpinejs/cdn.min.js" defer></script>
```

**_ViewImports.cshtml**
```cshtml
@addTagHelper *, NetOpenEditor
```

**Edit.cshtml**
```cshtml
<form method="post">
    ...campos del encabezado...
    <netopen-editor editor-id="journal-lines" rows="Model.Lines" name-prefix="Lines" />
    <button type="submit">Guardar</button>
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
document.addEventListener('noe:change', e => { /* e.detail.totals, e.detail.rows */ });
</script>
```

El controller recibe `List<JournalLine> Lines` bindeada por índice, exactamente como con inputs a mano.

## Columnas

| Método | Efecto |
|---|---|
| `.Column(l => l.Prop, c => ...)` | Tipo inferido: `string`→texto, `int`→entero, `decimal`→decimal, `DateOnly`→fecha, `bool`→toggle. `Guid`/enum exigen `.Select()` o `.Lookup()` |
| `.Header(t)` `.Placeholder(t)` `.Width(css)` `.Align(CellAlign)` `.Required()` `.Total()` `.Decimals(n)` | Presentación y validación |
| `.Text()` `.Integer()` `.Decimal(n)` `.Date()` `.Toggle()` `.ReadOnly()` `.Hidden()` `.Select(options)` | Editor explícito |
| `.Lookup(url, lk => lk.ValueField().LabelField().Display().Label().Companion().MinLength().Debounce())` | Lookup remoto `GET url?term=` → JSON array |
| `.Computed("Field", "Header", c => c.Decimal(2).Total())` | Columna calculada en cliente (hook `compute`), nunca se postea |
| `.MinRows(n)` | Mínimo de filas al enviar |

TagHelper: `editor-id`, `rows`, `name-prefix` (default `Lines`), `hide-columns="A,B"` (se postean como hidden).

## Hooks y API

```js
NetOpenEditor.configure(id, {
  onCellChange(row, field, editor), compute(row, editor), totals(rows, editor) => ({ Field: n }),
  canRemove(row, editor), isLocked(row, editor), onRowAdded(row, editor), onRowRemoved(row, editor),
  onLookupSelected(row, field, item, editor)
});
const ed = NetOpenEditor.get(id);   // rows(), addRow(values), removeRow(i), set(row, field, v), focusCell(i, field), validate(), recalc(), totals, num(v), fmt(v, d)
```
Eventos DOM (burbujean): `noe:ready`, `noe:change` con `detail = { id, editor, rows, totals }`.

**Envío programático:** `form.submit()` no dispara `submit`, así que una vista con envío propio debe
llamar `NetOpenEditor.get(id).validate()` antes.

## Estilos

CSS propio con variables `--noe-*` y clases `.noe-*`, sin `!important`. Sobreescribe desde tu tema:
```css
.noe { --noe-focus: #2563eb; --noe-head-bg: #f3f4f6; }
```
El panel del lookup usa `position: fixed`, así que el editor puede vivir dentro de un card con `overflow-x: auto`.

### Tu propia hoja de estilos

```csharp
builder.Services.AddNetOpenEditor(o =>
{
    o.CssPath = "/css";                  // sirve wwwroot/css/netopeneditor-default.css desde tu app
    o.CssFilePrefix = "netopeneditor-";  // solo aplica con CssPath asignado
});
app.UseStaticFiles();                    // obligatorio en cuanto asignas CssPath
```

La ruta embebida sigue montada, así que puedes descargar el CSS base como punto de partida:
`curl http://localhost:5199/_noe/netopeneditor.css -o wwwroot/css/netopeneditor-default.css`.

### Tema oscuro

El editor trae paleta clara y oscura. La oscura se activa con la clase `.dark` en cualquier ancestro:
**la misma que NetOpenGrid pone y persiste** en `localStorage` bajo `netgrid:theme`.

El editor **no** sigue a `prefers-color-scheme` por su cuenta: un componente embebido tiene que verse
como la página que lo aloja, no como el sistema operativo de quien la abre (si no, un layout claro se
vería con el editor oscuro en cualquier máquina con tema oscuro). Si tu app sí quiere delegar en el
sistema, actívalo con la clase `noe-auto` en el editor o en un ancestro:

```html
<body class="noe-auto">   <!-- ahora sí: prefers-color-scheme decide -->
```

Para forzar claro dentro de un documento oscuro, `.noe-light` sobre el editor.

Para que tome los tokens de tu tema, apunta las variables:

```css
.noe {
  --noe-border: var(--color-neutral-200);
  --noe-head-bg: var(--color-neutral-50);
  --noe-input-bg: var(--color-white);
}
```

## Captura masiva

### Pegar desde Excel

Copiá un bloque de celdas y pegalo en cualquier celda del editor: llena hacia la derecha y hacia
abajo desde donde está el cursor, creando las filas que falten y dejando siempre la fila vacía al
final. Pegar **una sola** celda mantiene el comportamiento normal del navegador.

- El mapeo es posicional sobre las columnas visibles, así que la tabla de Excel se alinea con lo que
  ves en pantalla.
- Las columnas `ReadOnly`, `Computed` y `Hidden` descartan el valor pegado; las calculadas se
  recalculan solas.
- **Las columnas de lookup sí se resuelven**: el texto pegado se consulta contra el mismo endpoint
  del buscador y, si hay una coincidencia **exacta y única** con el `value`, el `label` o alguno de
  los *display fields*, la celda queda igual que si la hubieras elegido a mano (id, etiqueta,
  *companions* y el hook `onLookupSelected`). Los términos se deduplican —pegar 50 líneas con 8
  cuentas distintas son 8 peticiones—, van de 6 en 6 y se resuelven hasta 100 términos distintos por
  pegada. Si el texto no encuentra nada, o coincide con más de uno, la celda queda marcada con el
  texto pegado a la vista y el mensaje `lookup.notFound` / `lookup.ambiguous`, así que `validate()`
  lo bloquea al guardar.
- La resolución es posterior al pegado: verás un segundo `noe:change` cuando llegan esos valores.
- Pegar cierra el buscador de la celda que tuviera el foco, y `Esc` lo cierra desde cualquier celda.
- Los números aceptan separador de miles y coma decimal: `1.234,56` y `1,234.56` entran como
  `1234.56`. Las filas bloqueadas (`isLocked`) se saltan sin consumir una línea del bloque.
- Máximo 500 filas por pegada; al pasarse se pegan las primeras 500 y se muestra el mensaje
  `paste.truncated`.
- Se emite **un solo** `noe:change` por pegada, no uno por celda.

El parser es público por si el host lo necesita: `NetOpenEditor.parseClipboard(text)` devuelve
`{ rows, truncated }`.

### Atajos de fila

| Atajo | Acción |
|---|---|
| `Ctrl+D` | Duplica la fila actual debajo, con el cursor en la misma columna |
| `Ctrl+Supr` | Elimina la fila actual |
| `Ctrl+Enter` | Inserta una fila vacía encima |

Ninguno opera sobre la fila fantasma ni sobre filas bloqueadas. `MinRows` **no** impide borrar: se
valida al enviar, como el resto de la validación de cliente.

### Accesibilidad

El shell expone semántica de grid: `role="grid"` con `aria-rowcount`, `role="row"` con
`aria-rowindex` (el encabezado es 1), `role="columnheader"` + `scope="col"` en los encabezados y
`role="gridcell"` con `aria-colindex` en las celdas. Los inputs llevan `aria-required` en columnas
requeridas y, cuando hay error, `aria-invalid="true"` y `aria-describedby` apuntando al mensaje de
esa celda. El error de formulario es `role="alert"` y la fila de totales `aria-live="polite"`.

## Convivir con NetOpenGrid

Ambos componentes registran su comportamiento en `alpine:init`, así que conviven en una página con
estas reglas:

1. **Un solo Alpine**, al final del `<body>`, `defer`.
2. `netopengrid.js` (`GridAssetTags.Head`) y `netopeneditor.js` (`EditorAssetTags.Head`) en el
   `<head>`, ambos `defer`. El orden entre ellos da igual; los dos deben ir **antes** de Alpine.
3. htmx solo lo necesita el grid.
4. Si tu app ya carga htmx y Alpine de forma global (p. ej. en un partial de scripts compartido), basta
   sumar `EditorAssetTags.Head` al `_Layout`.

`samples/NetOpenEditor.WithGrid` muestra **el editor dentro del grid**: el grid (paquete público
`NetOpenGrid` 1.0.3) se renderiza como fragmento en la vista —sin iframes— y al desplegar un documento
sus líneas se editan en una fila de detalle del propio `<tbody>`, compartiendo un solo Alpine. Guardar
postea el binding nativo y refresca el total de esa fila. Va incluida en la solución, así que sus
tests corren con `dotnet test NetOpenEditor.slnx`.

### Totales en la página del host

`noe:change` se emite en cada cambio, pero un form cuyas líneas vienen precargadas (por `rows` o por
el data source) no dispara ninguno al abrirse. Para pintar totales propios, escucha **también**
`noe:ready`, que se emite una vez tras hidratar:

```js
const paint = (e) => { /* e.detail.totals, e.detail.rows */ };
document.addEventListener('noe:ready', paint);
document.addEventListener('noe:change', paint);
```

## Contrato de datos

- Cada fila real `i` postea `Lines[i].Campo` (índices contiguos; la fila fantasma no lleva `name`).
- Decimales: texto normalizado a `toFixed(n)` con punto al perder el foco y antes del submit.
- Toggle: checkbox `value="true"` + hidden `false`. Lookup: hidden con el valor. Calculada: nada.
- Errores: claves `Lines[i].Campo` → celda; `Lines[i]` → fila.

## Cargar las líneas desde un data source

La vista puede pasar las filas (`rows`) o dejar que el editor las pida a un data source registrado en
DI — el mismo patrón de data source que usa NetOpenGrid:

```csharp
public sealed class JournalLineSource : IEditorLineSource<JournalLine>
{
    public ValueTask<IReadOnlyList<JournalLine>> LoadAsync(string key, CancellationToken ct = default)
        => /* el repositorio del módulo: SQL, EF, lo que uses */;
}

services.AddNetOpenEditor()
    .AddEditor<JournalLine>("journal-lines", e => e./* columnas */)
        .FromSource<JournalLineSource>();     // scoped, keyed por el id del editor
```

```cshtml
<netopen-editor editor-id="journal-lines" key="@Model.Id" name-prefix="Lines" />
```

La clave es un `string` opaco: el source decide si es un `Guid`, un folio o una clave compuesta.

**Precedencia — importante para los forms con validación:**

| `rows` | `key` | Qué pasa |
|---|---|---|
| presente | ausente | Usa `rows`. **Nunca** consulta el source. |
| ausente | presente | Pide las líneas al source. |
| ausente | ausente | Renderiza sin filas. |
| presente | presente | Excepción: son dos formas de decir lo mismo. |
| ausente | presente, sin source | Excepción nombrando el editor y el tipo que falta. |

`rows` gana siempre porque un POST que vuelve con errores debe re-renderizar **lo que el usuario
capturó**, no lo que hay en la base. En la práctica: `key` en el GET, `rows` en el re-render.
Ver `samples/NetOpenEditor.Example/Views/Quote/Edit.cshtml`.

## Ejemplo y tests

```bash
./tools/run.sh                     # demo en http://localhost:5199 (home con / journal/edit · /quote/edit)
dotnet run --project samples/NetOpenEditor.Example   # equivalente, sin script
dotnet test                                                                       # unitarios + integración + E2E (Playwright)
pwsh tests/NetOpenEditor.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium   # una vez
```

## Licencia

MIT.
