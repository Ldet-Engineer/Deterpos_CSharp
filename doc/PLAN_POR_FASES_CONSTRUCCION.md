# 🗺️ Plan Maestro por Fases de Construcción: Deterpos ERP

Este documento establece la secuencia cronológica, modular y de ingeniería para construir **Deterpos ERP** de forma ordenada, modular y libre de retrabajos.

---

## 1. Filosofía de Diseño: Vistas Principales y Ventanas Modales Modernas

Para que el sistema sea extremadamente ergonómico, rápido y visualmente integrado al estilo **Microsoft Office / Windows 11**:

### A. Vistas Principales (Espacio de Trabajo / Workspaces)
*   Se presentan en el contenedor principal central debajo del Ribbon (`ContentControl` con `UserControl`).
*   Se reservan para listas maestras, paneles de trabajo y tablas con filtros:
    *   Grilla de Productos e Inventario.
    *   Directorio de Clientes.
    *   Libros Fiscales.
    *   Terminal POS.

### B. Ventanas Modales Modernas (Dialogs & Overlays)
*   **Recomendación:** Se implementarán como **ventanas modales flotantes elegantes** (o tarjetas tipo `ContentDialog` con fondo oscuro traslúcido atenuador / backdrop blur).
*   **Casos de uso ideales:**
    1.  **Nuevo / Editar Cliente:** Desplegable tanto desde el directorio como en caliente desde la misma pantalla de ventas del POS sin salir de la caja.
    2.  **Nuevo / Editar Producto:** Formulario limpio con tabs (Datos Generales, Costos y Precios Multimoneda, Alícuota SENIAT, Control de Stock).
    3.  **Pasarela de Pago Split (Cobro Mixto en POS):** Ventana modal de alta concentración donde se ingresan los pagos en USD, Bs. Pago Móvil, Punto y se muestra el vuelto en tiempo real.
    4.  **Arqueo Ciego de Caja:** Matriz de denominaciones de billetes (1$, 5$, 10$, 20$, 50$, 100$ y Bs.) para conteo físico.
    5.  **Ajuste Rápido de Tasa del Día:** Modal accesible desde la barra superior QAT con tecla rápida.

---

## 2. Mapa de Dependencias Lógicas: ¿Por qué este orden?

Un error común en el desarrollo de software es comenzar por la pantalla de ventas (POS) sin tener los cimientos. En un ERP bimonetario:
1. El **POS** no puede facturar sin **Productos**.
2. Los **Productos** no pueden calcular sus precios en Bolívares sin las **Tasas de Cambio**.
3. El **POS** no puede abrirse si la **Caja** no ha iniciado su turno.
4. Las **Cuentas por Cobrar** dependen de los **Clientes** y las ventas a crédito.

Por ende, el orden de construcción debe ser estrictamente de menor a mayor dependencia.

---

## 3. Desglose Fase por Fase

```
┌────────────────────────────────────────────────────────────────────────┐
│ FASE 1: Motor Multimoneda y Tasas de Cambio (Núcleo Financiero)        │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 2: Inventario, Categorías y Catálogo de Productos                 │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 3: Directorio de Clientes y Proveedores (Entidades Comerciales)   │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 4: Control de Caja, Aperturas y Arqueo Ciego por Denominación     │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 5: Terminal Punto de Venta (POS F1) y Cobro Fraccionado Mixto     │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 6: Devoluciones y Notas de Crédito Fiscales SENIAT                │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 7: Compras, Recepción de Mercancía y Costeo Promedio USD          │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 8: Cuentas por Cobrar (CxC) y Cuentas por Pagar (CxP)             │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 9: Gastos Operativos y Salidas de Caja Chica                      │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 10: Libros Oficiales SENIAT (Ventas/Compras) y Reportes Excel     │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 11: Usuarios, Roles, Seguridad y Auditoría Operativa              │
├────────────────────────────────────────────────────────────────────────┤
│ FASE 12: Dashboard Ejecutivo en Tiempo Real y Cierre del Sistema       │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 📌 FASE 1: Motor Multimoneda y Tasas de Cambio
*Objetivo:* Establecer la base de conversión cambiaria en tiempo real para todo el ERP.
*   **Vistas y Formularios:**
    *   *Vista Principal:* Panel de Control de Monedas (USD Base, VES Primaria, EUR/COP conmutables).
    *   *Modal Moderno:* "Registrar / Actualizar Tasa del Día" (Tasa Oficial BCV, Paralelo, Euro, Peso).
    *   *Modal Moderno:* "Calculadora Multimoneda Rápida".
    *   *Histórico:* Grilla con trazabilidad cronológica de variaciones de tasa.
*   **Lógica / Backend:** `ICurrencyService`, persistencia en tabla `Currencies` y `ExchangeRates` en PostgreSQL.

---

### 📌 FASE 2: Inventario, Categorías y Catálogo de Productos
*Objetivo:* Maestro de artículos con costeo en USD, cálculo dinámico de PVP y alícuotas de IVA del SENIAT.
*   **Vistas y Formularios:**
    *   *Vista Principal:* Grilla moderna de productos con buscador instantáneo (por nombre, código o código de barras), filtros por categoría y badges de stock.
    *   *Modal Moderno:* "Nuevo / Editar Producto" (Código, Barras, Nombre, Categoría, Costo USD, % Margen, PVP USD, PVP VES calculado al vuelo, Alícuota SENIAT 16/8/0%, Stock Mínimo).
    *   *Modal Moderno:* "Gestión de Categorías y Marcas".
    *   *Modal Moderno:* "Ajuste Rápido de Stock / Merma".
*   **Lógica / Backend:** `IProductService`, cálculo automático de margen de ganancia, validación de códigos de barras únicos.

---

### 📌 FASE 3: Clientes y Proveedores
*Objetivo:* Gestión de los contactos comerciales para facturación y cuentas corrientes.
*   **Vistas y Formularios:**
    *   *Vista Principal:* Directorio de Clientes con saldo pendiente en USD y límite de crédito.
    *   *Modal Moderno:* "Nuevo / Editar Cliente" (Cédula/RIF V/E/J/G, Nombre, Teléfono, Dirección fiscal, Límite de crédito en USD).
    *   *Vista Principal:* Directorio de Proveedores con condición de pago y cuentas bancarias.
    *   *Modal Moderno:* "Nuevo / Editar Proveedor".
*   **Lógica / Backend:** Validación formal de RIF/Cédula venezolana, control de deudas.

---

### 📌 FASE 4: Control de Caja, Turnos y Arqueo Ciego
*Objetivo:* Gestión del efectivo de la estación de trabajo y prevención de fugas de dinero.
*   **Vistas y Formularios:**
    *   *Modal Moderno:* "Apertura de Caja" (Fondo inicial en efectivo USD y Bolívares).
    *   *Modal Moderno:* "Movimiento de Caja Chica" (Entrada / Salida justificada).
    *   *Modal Moderno:* "Arqueo Ciego de Caja" (El cajero cuenta y declara cantidad de billetes de 1, 5, 10, 20, 50, 100 USD y billetes de Bs. sin ver el saldo del sistema).
    *   *Vista:* Reporte de Conciliación de Arqueo y Descuadre de Turno.
*   **Lógica / Backend:** `ICashRegisterService`, registro en `CashShifts` y vinculación a `TerminalIdentifier`.

---

### 📌 FASE 5: Terminal Punto de Venta (POS F1) y Cobro Fraccionado
*Objetivo:* Facturación de alta velocidad táctil/escáner con pasarela de cobro mixto.
*   **Vistas y Formularios:**
    *   *Vista Principal POS:* Pantalla completa con buscador por código de barras / lector láser, tabla de items, teclado numérico en pantalla, totales grandes en USD y VES a tasa BCV.
    *   *Modal Moderno:* "Cobro Split / Multimoneda" (Agregar pagos: Efectivo USD, Pago Móvil VES, Punto Débito VES, Zelle USD, etc., cálculo automático de faltante y vuelto inteligente en la moneda deseada).
    *   *Modal Rápido:* "Crear Cliente al Vuelo" (para no abandonar la cola de cobro).
*   **Lógica / Backend:** `ISaleService`, cálculo de IGTF (3%), generación del comprobante y disparo a la impresora fiscal.

---

### 📌 FASE 6: Devoluciones y Notas de Crédito Fiscales SENIAT
*Objetivo:* Cumplimiento de la Providencia 00071 para devoluciones inmutables.
*   **Vistas y Formularios:**
    *   *Vista:* Historial de facturas emitidas con filtro por fecha, cliente o número fiscal.
    *   *Modal Moderno:* "Generar Nota de Crédito" (Selección de renglones a devolver, motivo, cálculo de IVA e IGTF a reversar).
*   **Lógica / Backend:** Reingreso automático a stock, emisión de comprobante fiscal con mención a la factura afectada.

---

### 📌 FASE 7: Compras y Recepción de Mercancía
*Objetivo:* Abastecimiento del negocio y actualización de costos de reposición.
*   **Vistas y Formularios:**
    *   *Vista Principal:* Historial de compras a proveedores.
    *   *Modal / Formulario:* "Registrar Compra / Factura de Proveedor" (Renglones, costo de compra, flete, cálculo de nuevo costo promedio ponderado en USD).
*   **Lógica / Backend:** Aumento de stock en PostgreSQL y registro en Cuentas por Pagar.

---

### 📌 FASE 8: Cuentas por Cobrar (CxC) y Cuentas por Pagar (CxP)
*Objetivo:* Libro de créditos blindado contra la inflación.
*   **Vistas y Formularios:**
    *   *Vista Principal:* Cartera de Clientes con facturas a crédito pendientes expresadas en USD.
    *   *Modal Moderno:* "Registrar Abono / Cobro de Cliente" (Recibe USD o Bolívares a la tasa del día del cobro).
    *   *Vista Principal:* Cuentas por Pagar a Proveedores y programación de pagos.
*   **Lógica / Backend:** Cancelación parcial o total de facturas, cálculo de diferencial cambiario.

---

### 📌 FASE 9: Gastos Operativos y Caja Chica [COMPLETADA 100%]
*Objetivo:* Control de desembolsos administrativos que no son costo de mercancía.
*   **Vistas y Formularios:**
    *   *Vista:* Lista cronológica de egresos por categoría (`OperatingExpensesView.xaml`).
    *   *Modal Moderno:* "Registrar Gasto Operativo" (`ExpenseDialog.xaml` - Categoría, Nómina, Alquiler, Servicios, Monto, Moneda, Comprobante).
*   **Lógica / Backend:** `IExpenseService`, `ExpenseService.cs`, deducción automática de caja si egresa de turno abierto, trazabilidad bancaria y bimonetaria.

---

### 📌 FASE 10: Libros Oficiales SENIAT y Reportes Ejecutivos [COMPLETADA 100%]
*Objetivo:* Cumplimiento tributario y análisis de inteligencia de negocio.
*   **Vistas y Formularios:**
    *   *Vista Principal:* `FiscalReportsView.xaml` con 4 pestañas interactivas.
    *   *Vista / Generador:* **Libro de Ventas Fiscal del IVA** (con todas las columnas obligatorias del Art. 56 y Providencia 00071).
    *   *Vista / Generador:* **Libro de Compras Fiscal del IVA**.
    *   *Resumen Tributario Forma 30:* Cálculo de Débito Fiscal, Crédito Fiscal, IVA a Pagar o Excedente de Crédito Fiscal.
    *   *Reportes de Inteligencia:* Ventas por período, productos más vendidos, reporte de ganancias brutas reales y medios de pago.
    *   *Exportador:* Generación nativa en formato CSV estructurado con codificación UTF-8 BOM compatible con Microsoft Excel.
*   **Lógica / Backend:** `IFiscalBookService`, `FiscalBookService.cs`, consultas agrupadas de alto rendimiento en PostgreSQL.

---

### 📌 FASE 11: Usuarios, Roles y Seguridad [COMPLETADA 100%]
*Objetivo:* Blindaje del sistema contra fraudes y control de accesos.
*   **Vistas y Formularios:**
    *   *Vista Principal:* `UsersManagementView.xaml` con tarjetas métricas, búsqueda y acciones rápidas.
    *   *Modal Moderno:* `UserEditDialog.xaml` (Crear y editar usuario, asignación de rol y validación).
    *   *Modal:* `UserPermissionsDialog.xaml` (Matriz granular de 13 facultades operativas y fiscales).
    *   *Modal:* `AuditLogDialog.xaml` (Visor de pistas de auditoría de seguridad).
*   **Lógica / Backend:** `IUserService`, `UserService.cs`, hashing criptográfico PBKDF2 (SHA-256 con 100.000 iteraciones y sal aleatoria), tablas `Users`, `UserPermissions`, `AuditLogs` en PostgreSQL.

---

### 📌 FASE 12: Dashboard Ejecutivo y Puesta a Punto Final [COMPLETADA 100%]
*Objetivo:* Consolidación de indicadores clave, inteligencia gerencial y experiencia unificada en tiempo real.
*   **Vistas y Formularios:**
    *   *Dashboard Dinámico en Vivo:* `MainWindow.xaml` con arquitectura responsiva y diseño Fluent Office Windows 11.
    *   *Tarjetas KPI Primarias:* Ventas del Día ($ USD), Ventas Fiscales Legales (Bs. VES), Tasa Oficial BCV activa y Estado de Impresora Fiscal / Transmisión DTD SENIAT.
    *   *Píldoras Operativas de Flujo:*
        *   Comprobantes Emitidos Hoy (Facturas vs Notas de Crédito).
        *   Cartera Activa de Cuentas por Cobrar (CxC en USD y contravalor en VES).
        *   Compromisos con Proveedores de Cuentas por Pagar (CxP en USD y contravalor en VES).
        *   Gastos Operativos Acumulados del Mes (USD y VES).
    *   *Gráfico Semanal de Tendencia de Facturación:* Componente nativo XAML en barras con escalado dinámico de porcentajes para los últimos 7 días.
    *   *Panel Multimoneda y Resumen Fiscal:* Monedas configuradas (USD, VES, EUR, COP) con botón de ajuste rápido y credenciales tributarias activas.
    *   *Grilla Plana de Alertas de Stock Mínimo:* Monitoreo en vivo de inventario crítico con insignias visuales (Agotado / Crítico) y enlace directo al catálogo.
    *   *Grilla Plana de Comprobantes Recientes:* Registro en vivo de últimas facturas y notas de crédito emitidas con enlace a documentos fiscales.
*   **Lógica / Backend:**
    *   `IDashboardService.cs` y `DashboardService.cs` en Infraestructura con agregaciones optimizadas en PostgreSQL.
    *   Integración en `MainViewModel.cs` con colecciones observables (`DashboardWeeklySales`, `DashboardLowStock`, `DashboardRecentSales`) y comando `RefreshDashboardCommand`.
    *   Diseño plano, moderno, sin biseles ni estilos 3D obsoletos.

---

## 🏆 ESTADO DEL PLAN MAESTRO: 12 DE 12 FASES COMPLETADAS (100%)
Todas las fases de ingeniería, arquitectura de dominio, reglas legales SENIAT (Providencias 00071, 0141, 00013 IGTF 3%, 00032), multimoneda estricta (cero float/double) y diseño visual empresarial Fluent han sido culminadas con éxito.
