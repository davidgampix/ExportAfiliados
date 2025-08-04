#!/usr/bin/env python3
"""
Script para exportar datos de afiliados a Excel con indicador de progreso
Conecta a SQL Server y ejecuta el stored procedure GetHierarchicalPlayersEmailVerified
"""

import pyodbc
import pandas as pd
from datetime import datetime
import sys
import os
import gc
import threading
import time

# Configuración de conexión
SERVER = '54.226.82.137'
DATABASE = 'SportsBet_Afiliados'
USERNAME = 'SportsBetLogin'
PASSWORD = '8B24BDF8-9541-47F8-9957-B63DD87FEFCE'

# Configurar colores para Windows
os.system('color')

class Colors:
    """Códigos de color ANSI"""
    RESET = '\033[0m'
    BOLD = '\033[1m'
    GREEN = '\033[92m'
    BLUE = '\033[94m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    CYAN = '\033[96m'
    MAGENTA = '\033[95m'
    WHITE = '\033[97m'

def print_banner():
    """Muestra el banner del programa"""
    banner = f"""{Colors.CYAN}{Colors.BOLD}
╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║      ███████╗██╗  ██╗██████╗  ██████╗ ██████╗ ████████╗                 ║
║      ██╔════╝╚██╗██╔╝██╔══██╗██╔═══██╗██╔══██╗╚══██╔══╝                 ║
║      █████╗   ╚███╔╝ ██████╔╝██║   ██║██████╔╝   ██║                    ║
║      ██╔══╝   ██╔██╗ ██╔═══╝ ██║   ██║██╔══██╗   ██║                    ║
║      ███████╗██╔╝ ██╗██║     ╚██████╔╝██║  ██║   ██║                    ║
║      ╚══════╝╚═╝  ╚═╝╚═╝      ╚═════╝ ╚═╝  ╚═╝   ╚═╝                    ║
║                                                                          ║
║        █████╗ ███████╗██╗██╗     ██╗ █████╗ ██████╗  ██████╗ ███████╗   ║
║       ██╔══██╗██╔════╝██║██║     ██║██╔══██╗██╔══██╗██╔═══██╗██╔════╝   ║
║       ███████║█████╗  ██║██║     ██║███████║██║  ██║██║   ██║███████╗   ║
║       ██╔══██║██╔══╝  ██║██║     ██║██╔══██║██║  ██║██║   ██║╚════██║   ║
║       ██║  ██║██║     ██║███████╗██║██║  ██║██████╔╝╚██████╔╝███████║   ║
║       ╚═╝  ╚═╝╚═╝     ╚═╝╚══════╝╚═╝╚═╝  ╚═╝╚═════╝  ╚═════╝ ╚══════╝   ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝{Colors.RESET}
"""
    print(banner)
    print(f"{Colors.WHITE}📊 Exportador de datos de jugadores de afiliados{Colors.RESET}")
    print(f"{Colors.YELLOW}🔗 Conecta a SQL Server y genera reportes Excel{Colors.RESET}\n")

class LoadingIndicator:
    """Clase para mostrar indicador de carga animado"""
    def __init__(self, message="Procesando"):
        self.message = message
        self.running = False
        self.thread = None
        
    def start(self):
        """Inicia el indicador de carga"""
        if not self.running:
            self.running = True
            self.thread = threading.Thread(target=self._animate)
            self.thread.daemon = True
            self.thread.start()
    
    def stop(self):
        """Detiene el indicador de carga"""
        self.running = False
        if self.thread:
            self.thread.join()
        # Limpiar línea
        print("\r" + " " * 80 + "\r", end="", flush=True)
    
    def _animate(self):
        """Animación del indicador"""
        chars = "⠧⠦⠤⠠⠡⠃⠇⠧"
        i = 0
        while self.running:
            print(f"\r{Colors.CYAN}{chars[i % len(chars)]}{Colors.RESET} {Colors.WHITE}{self.message}...{Colors.RESET}", end="", flush=True)
            time.sleep(0.15)
            i += 1

def conectar_db():
    """Establece conexión con SQL Server con timeouts optimizados"""
    try:
        connection_string = (
            f'DRIVER={{ODBC Driver 17 for SQL Server}};'
            f'SERVER={SERVER};'
            f'DATABASE={DATABASE};'
            f'UID={USERNAME};'
            f'PWD={PASSWORD};'
            f'Connection Timeout=30;'
            f'Command Timeout=600'
        )
        conn = pyodbc.connect(connection_string)
        print(f"{Colors.GREEN}🟢 Conexión establecida exitosamente{Colors.RESET}")
        return conn
    except Exception as e:
        print(f"{Colors.RED}❌ Error conectando a la base de datos: {e}{Colors.RESET}")
        return None

def ejecutar_stored_procedure(conn, root_affiliate):
    """Ejecuta el stored procedure con indicador de progreso"""
    try:
        cursor = conn.cursor()
        
        # Iniciar indicador de carga
        loader = LoadingIndicator("Ejecutando stored procedure")
        loader.start()
        
        start_time = datetime.now()
        
        # Ejecutar stored procedure
        sql = "{CALL [_V2_].[GetHierarchicalPlayersEmailVerified](?)}"
        cursor.execute(sql, root_affiliate)
        
        # Mover al primer result set que tenga datos
        while cursor.description is None:
            if not cursor.nextset():
                break
        
        loader.stop()
        
        # Verificar que hay datos
        if cursor.description is None:
            print(f"{Colors.RED}❌ El stored procedure no retornó datos{Colors.RESET}")
            return None
            
        columns = [column[0] for column in cursor.description]
        print(f"{Colors.GREEN}✅ SP ejecutado ({datetime.now() - start_time}){Colors.RESET}")
        print(f"{Colors.BLUE}  📋 Columnas encontradas: {len(columns)}{Colors.RESET}")
        
        # Procesar datos
        loader = LoadingIndicator("Procesando datos")
        loader.start()
        
        data_chunks = []
        chunk_size = 50000
        total_rows = 0
        
        while True:
            rows = cursor.fetchmany(chunk_size)
            if not rows:
                break
            data_chunks.extend(rows)
            total_rows += len(rows)
            
            if total_rows % 100000 == 0:
                loader.stop()
                print(f"{Colors.MAGENTA}📊 Procesados {total_rows:,} registros{Colors.RESET}")
                loader = LoadingIndicator(f"Procesando datos ({total_rows:,}+ registros)")
                loader.start()
        
        loader.stop()
        
        if not data_chunks:
            print(f"{Colors.RED}❌ No se obtuvieron datos del stored procedure{Colors.RESET}")
            return None
        
        # Crear DataFrame
        print(f"{Colors.YELLOW}🗺 Creando DataFrame con {total_rows:,} registros...{Colors.RESET}")
        df_loader = LoadingIndicator("Creando DataFrame")
        df_loader.start()
        
        df = pd.DataFrame.from_records(data_chunks, columns=columns)
        
        df_loader.stop()
        
        # Liberar memoria
        del data_chunks
        gc.collect()
        
        elapsed_time = datetime.now() - start_time
        print(f"{Colors.GREEN}✅ Proceso completado:{Colors.RESET}")
        print(f"{Colors.BLUE}  📈 Registros: {len(df):,}{Colors.RESET}")
        print(f"{Colors.BLUE}  ⏱️ Tiempo total: {elapsed_time}{Colors.RESET}")
        
        return df
        
    except pyodbc.DatabaseError as e:
        if 'loader' in locals():
            loader.stop()
        print(f"✗ Error de base de datos: {e}")
        return None
    except Exception as e:
        if 'loader' in locals():
            loader.stop()
        print(f"✗ Error ejecutando stored procedure: {e}")
        return None

def generar_excel(df, root_affiliate):
    """Genera archivo Excel con indicador de progreso"""
    try:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"afiliados_export_{root_affiliate}_{timestamp}.xlsx"
        filepath = os.path.join(os.path.dirname(__file__), filename)
        
        print(f"{Colors.BLUE}Generando Excel: {Colors.BOLD}{filename}{Colors.RESET}")
        
        excel_loader = LoadingIndicator(f"Generando Excel ({len(df):,} registros)")
        excel_loader.start()
        
        start_time = datetime.now()
        
        try:
            with pd.ExcelWriter(filepath, engine='xlsxwriter') as writer:
                df.to_excel(writer, sheet_name='Jugadores', index=False)
                
                workbook = writer.book
                worksheet = writer.sheets['Jugadores']
                
                # Formato de encabezados
                header_format = workbook.add_format({
                    'bold': True,
                    'text_wrap': True,
                    'valign': 'top',
                    'fg_color': '#D7E4BC',
                    'border': 1
                })
                
                # Aplicar formato
                for col_num, value in enumerate(df.columns.values):
                    worksheet.write(0, col_num, value, header_format)
                    max_length = max(len(str(value)), 15)
                    worksheet.set_column(col_num, col_num, min(max_length, 50))
                
        except ImportError:
            with pd.ExcelWriter(filepath, engine='openpyxl') as writer:
                df.to_excel(writer, sheet_name='Jugadores', index=False)
                
                worksheet = writer.sheets['Jugadores']
                for column in worksheet.columns:
                    max_length = max(len(str(cell.value or '')) for cell in column)
                    adjusted_width = min(max_length + 2, 50)
                    worksheet.column_dimensions[column[0].column_letter].width = adjusted_width
        
        excel_loader.stop()
        
        elapsed_time = datetime.now() - start_time
        file_size = os.path.getsize(filepath) / (1024 * 1024)
        
        print(f"{Colors.GREEN}🎉 Excel generado:{Colors.RESET}")
        print(f"{Colors.BLUE}  📄 Archivo: {Colors.BOLD}{filename}{Colors.RESET}")
        print(f"{Colors.BLUE}  📊 Tamaño: {file_size:.1f} MB{Colors.RESET}")
        print(f"{Colors.BLUE}  ⏱️ Tiempo: {elapsed_time}{Colors.RESET}")
        
        return filepath
        
    except Exception as e:
        if 'excel_loader' in locals():
            excel_loader.stop()
        print(f"✗ Error generando Excel: {e}")
        return None

def main():
    """Función principal"""
    print_banner()
    
    root_affiliate = input(f"{Colors.CYAN}{Colors.BOLD}🎯 Ingrese el nombre del afiliado raíz: {Colors.RESET}").strip()
    
    if not root_affiliate:
        print(f"{Colors.RED}❌ Debe ingresar un nombre de afiliado{Colors.RESET}")
        return
    
    print(f"\n{Colors.YELLOW}🔄 Procesando afiliado: {Colors.BOLD}{root_affiliate}{Colors.RESET}")
    print(f"{Colors.CYAN}" + "═" * 60 + f"{Colors.RESET}")
    
    # Conectar
    conn = conectar_db()
    if not conn:
        return
    
    try:
        # Ejecutar SP
        df = ejecutar_stored_procedure(conn, root_affiliate)
        if df is None or df.empty:
            print(f"{Colors.RED}❌ No se obtuvieron datos{Colors.RESET}")
            return
        
        # Generar Excel
        filepath = generar_excel(df, root_affiliate)
        if filepath:
            print(f"\n{Colors.GREEN}{Colors.BOLD}✅ Proceso completado exitosamente{Colors.RESET}")
            
            abrir = input(f"\n{Colors.YELLOW}📂 ¿Abrir archivo Excel? (s/n): {Colors.RESET}").strip().lower()
            if abrir == 's':
                try:
                    os.startfile(filepath)
                    print(f"{Colors.GREEN}🚀 Archivo abierto{Colors.RESET}")
                except:
                    print(f"{Colors.YELLOW}⚠️ No se pudo abrir automáticamente{Colors.RESET}")
        
    except KeyboardInterrupt:
        print(f"\n{Colors.RED}⛔ Proceso cancelado{Colors.RESET}")
    except Exception as e:
        print(f"{Colors.RED}❌ Error: {e}{Colors.RESET}")
    
    finally:
        try:
            if conn:
                conn.close()
                print(f"\n{Colors.GREEN}🔌 Conexión cerrada{Colors.RESET}")
        except:
            pass

if __name__ == "__main__":
    main()
