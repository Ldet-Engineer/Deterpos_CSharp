# 📘 Deterpos ERP - Plan Maestro y Especificación de Requerimientos

**Versión:** 1.0.0  
**Plataforma:** .NET 10 / C# WPF (Arquitectura en Capas)  
**Motor de Base de Datos:** PostgreSQL 16+  
**Interfaz de Usuario:** Ribbon Ejecutivo (Fluent.Ribbon + Fluent System Icons)  
**Mercado Objetivo:** Comercio, Retail y Empresas de Servicios (Multimoneda / Venezuela y Latinoamérica)

---

## 1. Visión y Propuesta de Valor

**Deterpos ERP** está concebido para ser un sistema de gestión empresarial de escritorio de alto rendimiento, visualmente distinguido y ergonómicamente superior a las soluciones tradicionales del mercado.

A diferencia de los sistemas legados que presentan interfaces rígidas y anticuadas, Deterpos ofrece:
1. **Experiencia de Usuario de Clase Mundial:** Interfaz fluida inspirada en la suite Microsoft Office (Word, Excel) mediante Ribbon nativo, menús Backstage contextuales y paleta corporativa sobria.
2. **Motor Bimonetario/Multimoneda Nativo:** Diseñado desde su núcleo para operar con **Dólar Estadounidense ($ USD)** como moneda base de anclaje de costos y precios, y **Bolívar Digital (Bs. VES)** como moneda operativa primaria para el mercado venezolano, con soporte extensible y configurable para **Euros (€ EUR)** y **Pesos Colombianos (Col$ COP)**.
3. **Arquitectura Desacoplada y Escalable:** Separación estricta de responsabilidades en 4 capas (Domain, Application, Infrastructure, WPF Presentation) que garantiza mantenibilidad, pruebas unitarias y fácil migración de componentes.
4. **Robustez ante el Escenario Operativo Real:** Soporte para cobros mixtos fraccionados (ej. parte en USD efectivo, parte en Pago Móvil, parte en Punto de Venta), arqueos ciegos de caja y tolerancia a fallos de conectividad.

---

## 2. Matriz de Módulos del Sistema

| # | Módulo | Propósito y Alcance Clave |
|---|---|---|
| **1** | **Dashboard (Panel Principal)** | Métricas en tiempo real, KPIs de ventas en USD y VES, paridad BCV activa, estado de caja y accesos directos operacionales. |
| **2** | **Punto de Venta (POS)** | Facturación ultra-rápida (teclado/escáner/táctil), atajo global `F1`, soporte para cobro en múltiples monedas y métodos simultáneos, cálculo de cambio/vuelto inteligente. |
| **3** | **Inventario y Catálogo** | Artículos con códigos de barras, múltiples unidades de medida, categorías, marcas, anclaje de costo en USD, margen de ganancia dinámico, alertas de stock mínimo e impresión de etiquetas. |
| **4** | **Compras y Proveedores** | Registro de compras con impacto en costo promedio en USD, órdenes de compra, control de cuentas por pagar a proveedores y recepción de mercancía. |
| **5** | **Gestión de Clientes** | Directorio fiscal (RIF/Cédula), límites de crédito en USD, historial comercial, direcciones de despacho y contacto. |
| **6** | **Apertura, Control y Arqueo de Caja** | Control de turnos por cajero, arqueo a ciegas (declaración de conteo físico por denominación en USD y VES), cortes X (parcial) y cortes Z (cierre final). |
| **7** | **Cuentas por Cobrar (CxC) y Pagar (CxP)** | Libro de deudas en moneda dura (USD) para proteger el valor de la cartera frente a la inflación, registro de abonos multimoneda con cálculo de diferencial cambiario. |
| **8** | **Tasas de Cambio y Multimoneda** | Mantenimiento de tasas del día (Oficial BCV, Paralelo, Euro, Peso), histórico cronológico con auditoría de cambios y calculadora de conversión rápida. |
| **9** | **Devoluciones y Notas de Crédito** | Devolución total o parcial de facturas, restitución automática a inventario y emisión de notas de crédito aplicables a futuras compras o reintegro de dinero. |
| **10** | **Gastos Operativos / Caja Chica** | Registro de salidas de dinero menores de la caja de turno o egresos administrativos categorizados (servicios, alquiler, nómina, viáticos). |
| **11** | **Reportes y Estadísticas** | Informes detallados de ventas por período, ventas por cajero, rentabilidad por producto, desglose de ingresos por método de pago y exportación nativa a Microsoft Excel y PDF. |
| **12** | **Usuarios, Roles y Seguridad** | Autenticación robusta, roles jerárquicos (Administrador, Supervisor, Cajero, Almacenista) con permisos granulares por acción y auditoría de operaciones críticas. |
| **13** | **Configuración General y Métodos de Pago** | Datos fiscales de la empresa, configuración de impresoras térmicas (58/80mm), catálogo de métodos de pago (Efectivo, Zelle, Pago Móvil, Punto, Transferencia) y reglas de IGTF. |

---

## 3. Modelo Multimoneda y Reglas de Negocio Financieras

### 3.1. Jerarquía de Monedas
*   **Moneda Base (USD):** Todos los costos de inventario, márgenes de ganancia, precios de lista y deudas a crédito se calculan y consolidan internamente en Dólares ($). No puede desactivarse ni eliminarse.
*   **Moneda Secundaria Primaria (VES):** Moneda de curso legal en Venezuela. Se recalcula en tiempo real multiplicando el valor base USD por la tasa oficial del día (BCV). No puede desactivarse.
*   **Monedas Secundarias Opcionales (EUR y COP):**
    *   Euro (€): Orientado a transacciones con divisas europeas o zonas comerciales específicas.
    *   Peso Colombiano (Col$): Vital para comercios en zonas fronterizas (Táchira, Zulia, Apure).
    *   Ambas pueden habilitarse o inhabilitarse con un switch en la configuración del Backstage sin afectar datos históricos.

### 3.2. Cobro Fraccionado / Pagos Mixtos (Split Tender)
Una de las mayores fuentes de fricción en los comercios venezolanos es la combinación de monedas al pagar. Deterpos implementará un motor de liquidación que:
1. Permite agregar N formas de pago a una misma venta (ejemplo: 20 USD en efectivo + 15 USD en Pago Móvil expresado en Bs. + 5 USD en Tarjeta de Débito VES).
2. Valida la tasa de cambio vigente al momento exacto de la transacción y congela la tasa en el documento para auditoría contable.
3. Soporta cálculo de vuelto/cambio en la moneda seleccionada por el cajero (ej. si pagó en divisas pero solo hay cambio en bolívares en caja).

---

## 4. Arquitectura de Software (Clean Architecture / 4 Capas)

```
                     ┌────────────────────────┐
                     │      Deterpos.WPF      │  (Capa de Presentación)
                     │  - Fluent.Ribbon UI    │  - Vistas y ViewModels (MVVM)
                     │  - Fluent System Icons │  - Notificaciones y Diálogos
                     └───────────┬────────────┘
                                 │
                     ┌───────────▼────────────┐
                     │  Deterpos.Application  │  (Casos de Uso y Lógica)
                     │  - Servicios de Negocio│  - DTOs y Validadores
                     │  - ICurrencyService    │  - Interfaces de Repositorios
                     └───────────┬────────────┘
                                 │
         ┌───────────────────────┴───────────────────────┐
         │                                               │
┌────────▼───────────────┐                     ┌─────────▼──────────────┐
│    Deterpos.Domain     │                     │ Deterpos.Infrastructure │  (Acceso a Datos)
│ - Entidades Puras      │                     │ - PostgreSQL (Npgsql)   │  - EF Core 9 DbContext
│ - Enums y Value Objects│                     │ - Repositorios          │  - Migraciones de BD
│ - Reglas Inmutables    │                     │ - Servicios de Impresión│  - Configuración Tablas
└────────────────────────┘                     └────────────────────────┘
```

---

## 5. Próximos Pasos en el Plan de Desarrollo

1. **Fase 1 (Actual):** Arquitectura base, diseño del Ribbon de Office y especificación documental.
2. **Fase 2:** Esquema de Base de Datos relacional en PostgreSQL (Tablas, Índices y Relaciones) con EF Core Migrations.
3. **Fase 3:** Servicio de Tasas de Cambio y Conversor Multimoneda en `Deterpos.Application`.
4. **Fase 4:** Módulo de Catálogo e Inventario (CRUD de Productos, Categorías y Listas de Precios).
5. **Fase 5:** Módulo de Caja (Apertura de turno, movimientos de caja chica y Arqueo Ciego).
6. **Fase 6:** Terminal Punto de Venta (POS F1) con interfaz táctil/escáner y pagos mixtos multimoneda.
7. **Fase 7:** Módulos de Clientes, Compras, Proveedores, CxC y CxP.
8. **Fase 8:** Reportes de ventas, Kardex, exportación a Excel y seguridad con roles de usuario.
