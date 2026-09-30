# NetOpenEditor.Example

App MVC mínima que hospeda el editor. Sirve de demo visual y de host para los tests de
integración (`WebApplicationFactory<Program>`) y E2E (Playwright sobre Kestrel).

## Correr

```bash
dotnet run --project samples/NetOpenEditor.Example
# http://localhost:5199
```

O desde la raíz del repo: `./tools/run.sh` (o `tools\run.cmd` en cmd).

## Qué demuestra

| Demo | Ruta | Qué muestra |
|---|---|---|
| Póliza contable | `/journal/edit` | Lookup remoto (`/accounts/lookup`), columnas con totales, hook `noe:change`, validación de partida doble en el servidor |
| Cotización | `/quote/edit` | Lookup de productos, columna entera, columnas **calculadas** (`Computed`) que no se postean |
| Resultado del post | vista `Result` | Confirma el binding nativo: el controller recibe `List<TLine>` ya poblada |

Registro de los editores: `SampleApp.cs`. Modelos: `Models/`. Datos en memoria: `Data/SampleCatalog.cs`.

## Qué mirar

- La última fila siempre está vacía; al escribir en ella aparece otra abajo.
- `Tab` / `Shift+Tab` / flechas navegan celdas; `Esc` revierte la celda en edición.
- `150,5` en un importe se normaliza a `150.50` al salir de la celda.
- Guardar con errores muestra los mensajes del servidor pegados a cada celda.
- En *ver código fuente*: los tres `<script type="application/json">` (config, filas, errores)
  son todo el estado que el servidor envía; el resto lo arma el runtime en el cliente.

Alpine.js está vendorizado en `wwwroot/lib/alpine.min.js` y se carga **después** de
`netopeneditor.js` (ambos `defer`); el JS y el CSS del editor los sirve el propio paquete
desde `/_netopeneditor/*`.
