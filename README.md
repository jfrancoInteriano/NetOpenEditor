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

Publica con NuGet Trusted Publishing (OIDC, sin API key guardada): requiere la política en nuget.org
(repo `NetOpenEditor`, workflow `publish.yml`, environment `nuget`), el secret `NUGET_USER` (usuario de
nuget.org) y el environment `nuget` en el repositorio.

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

## Filas fijas: `AllowAdd(false)`

Por defecto el editor mantiene una fila vacía al final que se vuelve real en cuanto el usuario
escribe en ella. Para editores cuyas líneas vienen dadas —una recepción contra una orden de compra,
una requisición que solo se despacha— declara que no se pueden agregar:

```csharp
.AddEditor<ReceiptLine>("receipt-lines", e => e
    .Column(l => l.ProductCode, c => c.ReadOnly())
    .Column(l => l.Received, c => c.Decimal(2).Total())
    .AllowAdd(false));
```

Con `AllowAdd(false)` **no hay fila fantasma** y ninguna de las vías de creación funciona: ni escribir
en la última celda, ni `Ctrl+Enter`, ni `Ctrl+D`, ni pegar más filas de las que existen (el bloque se
recorta a las filas disponibles), ni `NetOpenEditor.get(id).addRow(...)`, que devuelve `null` sin
tocar el estado. Las celdas siguen siendo editables y la validación sigue corriendo: es lo único que
se apaga.

Cuando el editor se queda sin filas, el cuerpo muestra el texto `rows.empty` ("Sin líneas") en vez de
quedar vacío.

**Borrar no cambia:** se sigue controlando con el hook `canRemove`. Y `AllowAdd(false)` convive con
`MinRows(n)`: el mínimo sigue siendo el suelo para borrar las líneas que llegaron del servidor. Si los
datos llegan por debajo de ese mínimo, `validate()` lo reporta al guardar, como siempre.

Ejemplo vivo: `/receipt/edit` en `samples/NetOpenEditor.Example`.

## Columna calculada editable: `Computed(...).Editable()`

A veces el usuario tiene el resultado delante —el total impreso de una factura— y lo que falta es el
dato de partida. Una columna calculada puede aceptar escritura sin dejar de ser calculada:

```csharp
.Computed("LineTotal", "Total", c => c.Decimal(2).Total().Editable())
```

**Sigue sin postearse** (no lleva `name`) y `compute` sigue siendo el único dueño de su valor. Lo que
el usuario teclea va al hook, con el texto crudo, y **el host decide qué escribir**:

```js
NetOpenEditor.configure('quote-lines', {
    onComputedInput(row, field, value, editor) {
        if (field !== 'LineTotal') return;
        const total = editor.num(value);
        const qty = editor.num(row.Quantity);
        if (qty > 0) row.UnitPrice = total / qty;      // se despeja el dato de partida
    },
    compute(row, editor) {
        row.LineTotal = editor.num(row.Quantity) * editor.num(row.UnitPrice);
    }
});
```

El editor **no** asigna `row[field]` con lo tecleado: si lo hiciera, el siguiente `recalc()` lo
pisaría y el host y la librería se pelearían por la misma celda. Al salir, la celda muestra el valor
recalculado con su formato, no el texto que se tecleó — así se ve de inmediato si lo que el host
despejó cuadra con lo que se escribió.

Pegar sobre una columna calculada sigue descartando el valor, editable o no: se recalcula sola.

## Un control extra en la celda: `Adornment`

Cuando una celda necesita algo más que su input —un botón que cambia la unidad de esa fila, por
ejemplo— la columna declara un adorno:

```csharp
.Column(l => l.DiscountPercent, c => c.Decimal(2).Adornment())
```

El editor emite un botón al final de la celda, pero **no decide nada sobre él**: su rótulo lo
devuelve el hook `adornmentLabel` y el clic ejecuta `onAdornment`. Sin rótulo no hay botón, así que
la misma columna puede mostrarlo solo en algunas filas.

```js
NetOpenEditor.configure('quote-lines', {
    adornmentLabel(row, field, editor) {
        return row.__host.discountUnit === 'money' ? currencyCode() : '%';
    },
    onAdornment(row, field, editor) {
        row.__host.discountUnit = row.__host.discountUnit === 'money' ? 'percent' : 'money';
    }
});
```

### `row.__host`: estado por fila que no se postea

Cada fila trae `__host`, una bolsa que es del host. El editor no lee nada de ella, no la serializa y
no la postea — ahí va lo que no es un valor de la línea: en qué unidad se está editando, un modo, una
marca. Al duplicar una fila (`Ctrl+D`) se copia, para que la copia conserve su modo.

### Cuando el rótulo depende de algo fuera del editor

Si el rótulo sale de la página (un selector de moneda en la cabecera), el editor no puede observarlo.
Avísale cuando cambie:

```js
document.getElementById('currency')
    .addEventListener('change', () => NetOpenEditor.get('quote-lines').refresh());
```

`refresh()` vuelve a pedir los rótulos y recalcula los totales. Sin eso, las filas ya marcadas
seguirían mostrando el rótulo anterior.

## Texto con autocompletado: `Suggest`

Cuando **el texto es el dato** y el buscador solo ayuda a escribirlo —pedir un artículo que quizá no
está en el catálogo— la columna es `Suggest`, no `Lookup`:

```csharp
.Column(l => l.Description, c => c
    .Header("Producto").Required().Placeholder("Buscar o escribir libre...")
    .Suggest("/products/suggest", s => s
        .LabelField("name")              // qué se escribe en la celda al elegir
        .Display("code", "name")         // qué se ve en el desplegable
        .MinLength(2)
        .Param("providerId", "[name='ProviderId']")))
```

La diferencia con `Lookup` es semántica, y manda en todo lo demás:

| | `Lookup` | `Suggest` |
|---|---|---|
| Valor posteado | la clave elegida | **el texto tecleado** |
| Texto sin elegir nada | se revierte al salir | **se conserva y se postea** |
| Al elegir | copia los `Companion` declarados | escribe el label y llama al hook `onSuggestionSelected` |
| Pegar | resuelve contra el endpoint | **es texto, sin petición remota** |

`LabelField` es obligatorio y no tiene valor por defecto: cada endpoint nombra su campo a su manera.

El hook decide qué rellena el item elegido, que es lo que `Companion` no puede hacer —copia sin
condiciones— cuando hay dos ids excluyentes o un campo que el usuario ya escribió:

```js
NetOpenEditor.configure('request-lines', {
    onSuggestionSelected(row, field, item, editor) {
        row.ProductId = item.productId || null;
        row.ServiceId = item.serviceId || null;
        if (!row.UnitOfMeasure) row.UnitOfMeasure = item.unitOfMeasure || '';
    }
});
```

Los ids van como columnas `Hidden`, así que se postean con la fila sin ocupar una celda. Ejemplo
vivo: `/request/edit` en `samples/NetOpenEditor.Example`.

## Lookup filtrado por la página: `Param`

Un buscador puede estrechar sus resultados con valores que el usuario sigue cambiando —el proveedor
de la cabecera, el almacén, la moneda— declarando parámetros que se **resuelven en cada búsqueda**:

```csharp
.Lookup("/products/lookup", lk => lk
    .ValueField("productId").LabelField("display")
    .Param("providerId", "[name='ProviderId']"))
```

El segundo argumento es un selector CSS: el editor lee el `value` de ese elemento justo antes de
buscar, así que cambiar la cabecera re-filtra al instante, sin recargar ni volver a registrar nada.
Un parámetro vacío **se omite** (un filtro vacío es "sin filtro", no "no coincide con nada").

Los parámetros viajan en **las dos** rutas que consultan el endpoint: el buscador y la resolución de
valores **pegados**. Si solo cubrieran la primera, el desplegable filtraría bien mientras un pegado
resolvería contra la lista completa, que es justo el caso que nadie ve hasta que ya está guardado.

Para valores que no están en el DOM, o que dependen de la fila, el hook `lookupParams` añade los
suyos:

```js
NetOpenEditor.configure('quote-lines', {
    lookupParams(row, field, editor) {
        return { warehouseId: currentWarehouse(), lineKind: row.Kind };
    }
});
```

Un editor que no declara parámetros ni usa el hook envía exactamente la misma petición que antes.

## Rangos numéricos: `Min` / `Max`

Las columnas `Integer` y `Decimal` aceptan un rango declarado:

```csharp
.Column(l => l.Received, c => c.Decimal(2).Min(0m).Max(10m))
```

Fuera de rango, la celda queda marcada con `min` / `max` ("Mínimo 0.00", "Máximo 10.00") al salir de
ella, y `validate()` lo bloquea al guardar. **El editor no reescribe lo que el usuario tecleó**: avisa,
no corrige en silencio. Si necesitas ajustar el valor, o un límite que dependa de la fila (por ejemplo
lo pendiente de cada renglón), eso sigue siendo trabajo del hook `onCellChange`.

Declarar `Min`/`Max` en una columna no numérica, o un `Min` mayor que el `Max`, falla al registrar el
editor.

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
