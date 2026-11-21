# 🏛️ Guía de Cumplimiento Fiscal y Homologación SENIAT para Deterpos ERP

Este documento recopila la normativa tributaria vigente en la República Bolivariana de Venezuela, las Providencias Administrativas del **SENIAT** y los requerimientos funcionales y técnicos que **Deterpos ERP** debe cumplir para ser un sistema 100% legal, homologable y apto para fiscalizaciones.

---

## 1. Marco Legal Vigente del SENIAT

| Instrumento Legal | Objeto y Alcance |
|---|---|
| **Providencia Administrativa SNAT/2011/00071** | Normas Generales de Emisión de Facturas y Otros Documentos (regula el contenido obligatorio de facturas, notas de crédito y débito). |
| **Providencia Administrativa SNAT/2014/00032** | Regula la emisión de facturas mediante **Sistemas Computarizados** y Formas Libres elaboradas por imprentas autorizadas. |
| **Providencia Administrativa SNAT/2018/0141** | Exigencias para **Máquinas Fiscales**, incluyendo la obligación del Dispositivo de Transmisión de Datos (DTD) al SENIAT y memoria de auditoría. |
| **Ley del IGTF (G.O. 6.687 / 2022) y Providencia 000013** | Impuesto a las Grandes Transacciones Financieras: **3% de percepción** en cobros con moneda extranjera (divisas o criptomonedas) en Sujetos Pasivos Especiales. |
| **Ley y Reglamento del IVA (Art. 56 a 78)** | Obligatoriedad de llevar y emitir los **Libros de Ventas y Compras Fiscales** mensuales y cronológicos. |

---

## 2. Los Dos Mecanismos de Facturación Válidos en Venezuela

En Venezuela un software ERP/POS **no puede imprimir una factura válida en papel térmico común desde una impresora genérica** por su propia cuenta. Para ser legal, Deterpos debe admitir uno o ambos de los siguientes esquemas:

### Esquema A: Impresoras Fiscales Homologadas (El estándar del Retail)
Es el método obligatorio para comercios masivos, supermercados, farmacias, tiendas y restaurantes.
*   **Cómo funciona:** Deterpos se comunica mediante drivers DLL con una máquina fiscal homologada (The Factory HKA / Bixolon, Bematech, Dascom Tally, Custom, Epson, PNP).
*   **Qué hace la máquina fiscal:** Graba la venta en su Memoria Fiscal física inalterable, imprime el ticket fiscal con logotipo fiscal del SENIAT y transmite los datos al SENIAT vía su dispositivo de transmisión (DTD).
*   **Requerimiento para Deterpos:** 
    *   Integración con el SDK fiscal (ej. `TFHKA.dll` / `BxlFiscal.dll`).
    *   Emisión de comandos de apertura fiscal, renglones con alícuotas de IVA (G, R, A, E), percepción de IGTF 3%, formas de pago y cierre de ticket.
    *   Módulo de **Reporte X** (lectura parcial sin cierre) y **Reporte Z** (cierre diario obligatorio que bloquea la facturación hasta el día siguiente).

### Esquema B: Formas Libres Computarizadas o Facturación Electrónica
Para mayoristas, empresas de servicios, distribuidoras o empresas que facturan en hojas tamaño carta.
*   **Formas Libres:** Papel bond preimpreso por una imprenta autorizada por el SENIAT que incluye el **Número de Control Fiscal** preimpreso. Deterpos debe registrar e imprimir el número de control correlativo sin saltos.
*   **Facturación Digital / Electrónica:** Integración mediante API con un Proveedor Autorizado de Facturación Electrónica por el SENIAT (PAFE, ej. The Factory HKA).

---

## 3. Campos y Datos Obligatorios en el Comprobante de Venta

Según la **Providencia 00071**, toda factura generada por Deterpos debe contener:

### 3.1. Datos del Emisor
*   Nombre o Razón Social de la empresa.
*   Número de RIF (Registro de Información Fiscal) del emisor.
*   Dirección fiscal completa de la casa matriz y de la sucursal donde se emite la factura.
*   Número de teléfono y datos de contacto.

### 3.2. Datos del Cliente / Receptor
*   Nombre y Apellido o Razón Social.
*   Cédula de Identidad o RIF con prefijo oficial (`V-`, `E-`, `J-`, `G-`, `P-`).
*   Dirección fiscal (obligatoria para ventas a empresas o compras a crédito; en tickets de caja rápida a personas naturales se permite cliente genérico de contado si no supera los límites del SENIAT).

### 3.3. Identificación del Documento
*   Palabra explícita: **"FACTURA"**, **"NOTA DE CRÉDITO"** o **"NOTA DE DÉBITO"**.
*   **Número de Factura:** Secuencial, único y sin saltos correlativos.
*   **Número de Control:** En formas libres, el número asignado por la imprenta autorizada.
*   **Serial Fiscal de la Máquina:** En impresoras fiscales (ej. `Z4A1234567`).
*   Fecha y hora exacta de emisión.

### 3.4. Detalle de Bienes y Alícuotas de IVA
Cada renglón facturado debe indicar claramente:
*   Descripción detallada del bien o servicio (no se permiten conceptos ambiguos como "Varios").
*   Cantidad y unidad de medida.
*   Precio unitario.
*   Alícuota de IVA aplicable:
    *   **G (General):** 16%
    *   **R (Reducida):** 8% (alimentos o bienes específicos según ley)
    *   **A (Adicional / Suntuario):** 31% (16% + 15% de lujo)
    *   **E (Exento / Exonerado):** 0% (medicamentos, alimentos de canasta básica)

### 3.5. Totales y Discriminación Tributaria
*   Total Ventas Exentas o Exoneradas.
*   Base Imponible General (16%) y Monto de IVA resultante.
*   Base Imponible Reducida (8%) y Monto de IVA.
*   Total Facturado en Bolívares (VES) de curso legal obligatorio.
*   Mención y desglose de referencia en Dólares ($ USD) a la tasa oficial del BCV vigente al momento de la venta.

### 3.6. IGTF (3% para Sujetos Pasivos Especiales)
Si la empresa es designada Contribuyente Especial por el SENIAT:
*   Discriminación de la base sujeta al 3% (monto recibido en efectivo USD, Zelle u otra divisa).
*   Monto en Bolívares y en Divisas percibido por concepto de IGTF (3%).

---

## 4. Gestión de Devoluciones y Anulaciones (Notas de Crédito)

> ⚠️ **Regla de Oro del SENIAT:** Ninguna factura emitida puede ser eliminada, borrada ni modificada en la base de datos de Deterpos.

*   Si un cliente devuelve mercancía o hubo un error de facturación, se emite obligatoriamente una **Nota de Crédito**.
*   La Nota de Crédito debe asociar obligatoriamente:
    *   El **Número de Factura afectada**.
    *   La **Fecha de emisión** de la factura original.
    *   El **Serial Fiscal** de la máquina donde se originó la venta.
*   El sistema restituye automáticamente las existencias al inventario en PostgreSQL y rebaja el débito fiscal en el Libro de Ventas.

---

## 5. Libros Fiscales Obligatorios de IVA (Art. 56 Ley del IVA)

Deterpos ERP debe incluir un módulo específico para generar e imprimir o exportar:

### 5.1. Libro de Ventas Fiscal
Columnas normativas obligatorias:
1. N° de Operación.
2. Fecha del Documento.
3. RIF o Cédula del Comprador.
4. Nombre o Razón Social del Comprador.
5. N° de Factura / Ticket Fiscal.
6. N° de Control (si aplica).
7. N° de Nota de Débito o Crédito.
8. N° de Factura Afectada.
9. Total Ventas incluyendo IVA.
10. Ventas Internas No Sujetas o Exentas.
11. Base Imponible Alícuota General (16%).
12. Débito Fiscal Alícuota General (16%).
13. Base Imponible y Débito Alícuotas Reducidas/Adicionales.
14. Base Imponible IGTF (3%) y Monto Percibido IGTF.

### 5.2. Libro de Compras Fiscal
Generado a partir del módulo de **Compras y Proveedores**:
*   Registro de facturas de compras recibidas de proveedores con su N° de RIF, N° de Factura y N° de Control.
*   Cálculo del Crédito Fiscal de IVA deducible para la declaración mensual o quincenal ante el portal del SENIAT.

---

## 6. Checklist de Homologación e Integración para Deterpos

- [x] **Cálculo Bimonetario Transparente:** La factura se liquida en moneda de curso legal (VES) y muestra la referencia en USD con la tasa oficial BCV.
- [x] **Inalterabilidad de Documentos:** Arquitectura con auditoría inmutable (las facturas no se borran; se generan notas de crédito).
- [ ] **Módulo Fiscal Abstraído (`IFiscalPrinterService`):**
  - Conector para impresoras **The Factory HKA** (Bixolon SRP-350 / HKA-80).
  - Conector para impresoras **Dascom / Bematech**.
  - Conector para **Formas Libres con N° de Control**.
- [ ] **Módulo de Reportes Fiscales X y Z:**
  - Botón en Ribbon: Corte X (lectura).
  - Botón en Ribbon: Corte Z (cierre diario fiscal).
- [ ] **Módulo de Libros Fiscales de IVA:**
  - Generador del Libro de Ventas y Compras mensual con exportación oficial a Excel / PDF.
- [ ] **Configuración de Contribuyente Especial / IGTF:**
  - Switch en Backstage para activar cobro de 3% IGTF en pagos en divisas.
