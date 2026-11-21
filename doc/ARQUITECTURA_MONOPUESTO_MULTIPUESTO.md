# 🖥️ Arquitectura Monopuesto vs. Multipuesto en Deterpos ERP

Este documento detalla la estrategia de despliegue y diseño técnico para que **Deterpos ERP** funcione inicialmente en **un solo equipo** (computador principal con PostgreSQL local) y pueda escalar fluidamente a **múltiples cajas o estaciones de trabajo en red local**, sin requerir cambios en el código fuente.

---

## 1. Topología del Sistema

### Escenario A: Monopuesto (Fase Actual de Desarrollo)
En esta fase, la computadora principal actúa simultáneamente como:
*   **Servidor de Base de Datos:** Aloja el servicio de PostgreSQL en `localhost:5432`.
*   **Estación de Trabajo / Caja 1:** Ejecuta la aplicación de escritorio Deterpos.WPF con el identificador `POS-01`.

```
┌────────────────────────────────────────────────────────┐
│             Computador Principal (Servidor)            │
│                                                        │
│   ┌──────────────────────┐    ┌────────────────────┐   │
│   │     Deterpos.WPF     │───>│   PostgreSQL 16    │   │
│   │   (Terminal POS-01)  │    │   (localhost:5432) │   │
│   └──────────────────────┘    └────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

---

### Escenario B: Multipuesto (Múltiples Cajas en Red Local LAN)
La computadora principal sigue alojando la base de datos central en PostgreSQL, pero ahora **Caja 2, Caja 3 y Administración** se conectan por red local (Wi-Fi o cable Ethernet).

```
                      ┌──────────────────────────────────────────────┐
                      │        Computador Principal (Servidor)       │
                      │                                              │
                      │   ┌────────────────────┐  ┌──────────────┐   │
                      │   │   PostgreSQL 16    │<─│ Deterpos.WPF │   │
                      │   │(192.168.1.50:5432) │  │  (Caja #1)   │   │
                      │   └─────────▲──────────┘  └──────────────┘   │
                      └─────────────┼────────────────────────────────┘
                                    │ Red Local (LAN)
             ┌──────────────────────┴──────────────────────┐
             │                                             │
┌────────────▼──────────────┐                ┌─────────────▼──────────────┐
│       Caja #2 (POS)       │                │      Administración        │
│    (Terminal POS-02)      │                │   (Reportes e Inventario)  │
│ Deterpos.WPF -> IP Server │                │ Deterpos.WPF -> IP Server  │
└───────────────────────────┘                └────────────────────────────┘
```

---

## 2. Pilares de Diseño para Soportar Múltiples Cajas

### 2.1. Archivo de Configuración de Terminal Local (`dbsettings.json`)
Cada equipo físico almacena su propia configuración en `%AppData%\Deterpos\dbsettings.json`:
*   **En la máquina Servidor:** `Server: "localhost"`, `TerminalIdentifier: "POS-01"`.
*   **En la Caja #2:** `Server: "192.168.1.50"`, `TerminalIdentifier: "POS-02"`.
*   **En la Caja #3:** `Server: "192.168.1.50"`, `TerminalIdentifier: "POS-03"`.

### 2.2. Aislamiento de Turnos y Arqueos por Caja (`CashRegisterId`)
*   Cada equipo opera sobre su propia `CashRegister` registrada en PostgreSQL.
*   Cuando la **Caja 1** abre turno (`CashShift`), su dinero en efectivo (USD y VES) está aislado del dinero de la **Caja 2**.
*   El cajero de la Caja 2 arquea y cierra su propio turno sin interferir con las operaciones o el dinero de las otras cajas.

### 2.3. Correlativos y Numeración de Facturas Fiscales
Para evitar que dos cajas intenten emitir la misma factura número `1001` al mismo segundo:
*   **Opción A (Recomendada en Venezuela):** Series por caja.  
    *   Caja 1 emite: `FAC-01-00000001`, `FAC-01-00000002`...
    *   Caja 2 emite: `FAC-02-00000001`, `FAC-02-00000002`...
*   **Opción B:** Secuencia atómica centralizada en PostgreSQL (`CREATE SEQUENCE invoice_number_seq`).

### 2.4. Control de Concurrencia de Inventario
*   Todas las cajas consultan el mismo catálogo centralizado en PostgreSQL.
*   Las ventas descuentan stock mediante transacciones ACID atómicas. Si dos cajas intentan vender la última unidad al mismo tiempo, el motor de PostgreSQL garantiza la integridad y previene sobreventas.

---

## 3. Hoja de Ruta para la Configuración Inicial

1. **Instalación y Verificación de PostgreSQL:**
   *   Instalar PostgreSQL en la máquina principal (o inicializar el servicio en el puerto `5432`).
   *   Crear la base de datos `deterpos_db` o permitir que `DatabaseConfigurationService.InitializeDatabaseAsync()` cree automáticamente las tablas y datos iniciales.
2. **Pestaña Backstage de Conexión:**
   *   Probar la conexión en vivo desde el botón "Probar Conexión PostgreSQL" implementado en el menú Archivo del Ribbon.
3. **Inicio del Desarrollo de Módulos Operativos:**
   *   Comenzar con la lógica del Catálogo de Productos y Monedas para cargar los primeros datos en PostgreSQL.
