# NetOpenEditor.WithGrid

**El editor dentro del grid.** Con el paquete público
[`NetOpenGrid` 1.0.3](https://www.nuget.org/packages/NetOpenGrid/):

- **NetOpenGrid** renderiza el listado de documentos como **fragmento** dentro de esta vista
  (`IGridRuntime.RenderFragmentAsync`, vía el TagHelper local `<grid-fragment grid-id="documents" />`).
  Sin iframes.
- Al pulsar *Editar líneas*, la fila **se despliega**: se inserta una fila de detalle en el `<tbody>`
  del propio grid y ahí dentro se renderiza **NetOpenEditor** con las líneas de ese documento.
- Guardar postea `lines[i].Campo` —el binding nativo del editor— y actualiza el total de esa fila del
  grid sin recargar la página.

## Correr

```bash
dotnet run --project samples/NetOpenEditor.WithGrid --urls http://localhost:5210
# http://localhost:5210/documents
```

## Cómo está montado

| Pieza | Dónde |
|---|---|
| Columna de acción: `<button class="doc-toggle" data-doc-id="…">` | `DemoApp.cs`, con `RawCellHtml` |
| Fragmento del editor para un documento | `GET /documents/{id}/editor` → `Views/Documents/_LinesEditor.cshtml` |
| Insertar/quitar la fila de detalle, guardar y refrescar el total | `<script>` de `Views/Documents/Index.cshtml` |
| Guardado | `POST /documents/{id}/lines`, que vincula `List<DocumentLine>` y devuelve el total nuevo |

Tres detalles que hacen que funcione, y que costaron depuración:

1. **La delegación de eventos va en `document`, no en el `<tbody>`.** htmx reemplaza ese `<tbody>`
   entero en cada página, filtro u orden; un listener atado a él muere en el primer swap.
2. **El toggle se busca por su clase** (`.doc-toggle`), no por `[data-doc-id]`: el `<form>` del
   editor también lleva ese atributo, y con el selector laxo cada clic dentro del editor se
   interpretaba como "colapsa esta fila".
3. **El fragmento se pide con `cache: 'no-store'`**, o al reabrir una fila el navegador sirve la
   copia anterior y no ves lo que acabas de guardar.

## Tests

Van con la solución: `dotnet test NetOpenEditor.slnx`. Cubren que al cargar no hay editor y que al
expandir aparece dentro del `<tbody>` del grid, que abrir otro documento cierra el anterior, que
volver a pulsar colapsa, que **los cinco** documentos abren con líneas editables, que guardar postea y
refresca el total de la fila, que la página carga un solo Alpine y un solo htmx, y que `.dark` cambia
también al editor incrustado.

## Assets

`_Layout.cshtml` emite una vez `GridAssetTags.Head(...)` y `EditorAssetTags.Head(...)` en el `<head>`,
y htmx y Alpine al final del body desde `/_netgrid/vendor/*` (los trae el paquete del grid; el editor
no los embebe). El tema del grid va embebido en 1.0.3, así que no hace falta `wwwroot` ni
`UseStaticFiles()`. El editor sigue la clase `.dark` que persiste el toggle del grid.
