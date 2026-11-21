# Deterpos ERP - Retail Multimoneda C# (.NET 10 + WPF)

Sistema ERP moderno de punto de venta (POS) y gestión comercial integral diseñado para el contexto retail de alta exigencia y economía multimoneda (USD Base, Bolívares VES oficial BCV, EUR, COP).

Desarrollado en **C# (.NET 10)** bajo una arquitectura limpia en 4 capas (Clean Architecture), interfaz rica y fluida con **WPF (Fluent Ribbon UI)** y base de datos relacional **PostgreSQL**.

---

## 🚀 Características Principales

### 1. Punto de Venta (POS) y Facturación Fiscal SENIAT
- Facturación rápida con lector de código de barras y teclado numérico.
- Discriminación automática de alícuotas de IVA (General 16%, Reducida 8%, Exento 0%).
- Cumplimiento de la **Providencia Administrativa 00013** del SENIAT: Percepción del 3% IGTF sobre pagos en divisas / efectivo extranjero.
- Emisión de comprobantes fiscales reglamentarios (Factura, Reporte X y Reporte Z).
- Pagos divididos y combinados (Efectivo USD, Efectivo Bs., Punto de Venta, Pago Móvil, Zelle).

### 2. Control de Caja y Arqueo Ciego Multimétodo
- Apertura y cierre de turnos por cajero.
- **Arqueo Ciego sin desglose forzoso de billetes:** Entrada directa de montos globales contados en Efectivo USD ($) y Efectivo Bs. (VES).
- **Conciliación de Cierres de Lote Bancarios:** Declaración de lotes de Punto de Venta (datáfono), Pago Móvil y Zelle.
- Cruce automático contra el saldo teórico del sistema y generación de acta formal inmutable con impresión en ticket térmico de 80mm.

### 3. Sincronización Automática de Tasas en Línea (BCV)
- Servicio de paridad cambiaria en tiempo real con arquitectura híbrida y fallback (DolarApi Venezuela oficial, scraping directo al portal web del BCV y API de paridades internacionales para COP/EUR).
- Sincronización a un clic con fecha valor legal y actualización instantánea de precios en todo el catálogo.

### 4. Inventario, Catálogo y Precios Oficiales
- Control de existencias con alertas de stock mínimo y valoración a costo promedio ponderado.
- Gestor dinámico de **Categorías y Marcas** conectado a PostgreSQL.
- **Lista Oficial de Precios Multimoneda:** Visualización bimonetaria con cálculo automático a tasa oficial BCV, márgenes de ganancia y exportación a Excel / CSV con UTF-8 BOM.
- **Impresión de Etiquetas de Código de Barras (WYSIWYG):** Previsualización gráfica en vivo, códigos de barras vectoriales (EAN-13 / Code 128) y emisión para impresoras térmicas Zebra / Xprinter.

### 5. Cuentas por Cobrar (CxC), Cuentas por Pagar (CxP) y Compras
- Gestión de cartera de clientes protegida en moneda dura (USD) con liquidación a tasa oficial BCV al momento del abono.
- Registro de compras a proveedores con alta rápida de proveedor y actualización automática de inventarios y costos.

---

## 🏛 Arquitectura del Proyecto

```
Deterpos_CSharp/
├── src/
│   ├── Deterpos.Domain/           # Entidades, Enums y Lógica de Dominio
│   ├── Deterpos.Application/      # DTOs, Interfaces de Servicios y Casos de Uso
│   ├── Deterpos.Infrastructure/   # Entity Framework Core 10, DbContext, PostgreSQL, APIs y Scraping
│   └── Deterpos.WPF/              # Interfaz Gráfica XAML, ViewModels (MVVM), Fluent Ribbon y Diálogos
├── doc/                           # Documentación técnica y especificaciones
├── .gitignore
├── README.md
└── Deterpos.slnx                  # Solución moderna de Visual Studio / .NET 10
```

---

## 🛠 Requisitos de Entorno

- **.NET SDK 10.0** (x64)
- **PostgreSQL 15+** (Instalación local o Docker container)
- **Windows 10 / 11** (Soporte completo para DPI Awareness PerMonitorV2)

---

## 💻 Compilación y Ejecución

```bash
# Restaurar dependencias
dotnet restore

# Compilar la solución en modo Debug o Release
dotnet build src/Deterpos.WPF/Deterpos.WPF.csproj -c Release

# Ejecutar el aplicativo
dotnet run --project src/Deterpos.WPF/Deterpos.WPF.csproj
```

---

## 📄 Licencia y Créditos

Desarrollado por **Ldet-Engineer**. Todos los derechos reservados.
