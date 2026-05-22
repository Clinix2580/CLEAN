# ClinicOS - Sistema de Gestión Clínica

Sistema de gestión clínica compliant con auditoría ISO, desarrollo en .NET 8.0 con cero advertencias de compilación.

## Características

- **Auditoría ISO**: Compilación con 0 advertencias y 0 errores
- **Seguridad**: Cumplimiento LFPDPPP (México) e HIPAA (USA)
- **Arquitectura**: Clean Architecture con Domain-Driven Design
- **Base de datos**: PostgreSQL con SSL/TLS
- **Offline-first**: Funcionamiento sin conexión a internet
- **Multi-idioma**: Español e Inglés

## Requisitos

- .NET 8.0 SDK
- PostgreSQL 14+
- Windows 10/11 (plataforma principal)

## Compilación

```bash
# Restaurar dependencias
dotnet restore Nuevo_Sistema.sln

# Compilar en Debug
dotnet build Nuevo_Sistema.sln --configuration Debug

# Compilar en Release
dotnet build Nuevo_Sistema.sln --configuration Release
```

## Pruebas

```bash
# Ejecutar pruebas
dotnet test Nuevo_Sistema.sln
```

## GitHub Actions

El proyecto incluye un workflow de GitHub Actions configurado para:

1. **Compilación automatizada**: Verifica que el proyecto compile sin errores
2. **Verificación de advertencias**: Falla si hay advertencias (requisito ISO)
3. **Ejecución de pruebas**: Corre tests automatizados
4. **Multi-configuración**: Prueba en Debug y Release

### Configuración en GitHub

```bash
# Agregar remote de GitHub
git remote add origin https://github.com/tu-usuario/ClinicOS_Clean.git

# Push al repositorio
git push -u origin main
```

El workflow se ejecutará automáticamente en cada push y pull request a las ramas `main` y `develop`.

## Estructura del Proyecto

```
src/
├── Core/
│   ├── Domain/           # Entidades y lógica de dominio
│   └── Application/     # Servicios de aplicación
└── Infrastructure/      # Implementación técnica (DB, logging, etc.)
tests/                   # Pruebas unitarias e integración
scripts/                 # Scripts de utilidad
installers/             # Instaladores InnoSetup
```

## Archivos de Configuración

- `.github/workflows/build-and-test.yml` - Workflow de GitHub Actions
- `.gitignore` - Archivos ignorados por Git
- `global.json` - Configuración global de .NET

## Auditoría ISO

El proyecto cumple con los requisitos de auditoría ISO:

✅ **Cero advertencias de compilación**  
✅ **Control de versiones con Git**  
✅ **CI/CD automatizado con GitHub Actions**  
✅ **Pruebas automatizadas**  
✅ **Documentación de cambios**  
✅ **Seguridad y cumplimiento normativo**

## Soporte

Para soporte técnico, consulte la documentación en `docs/` o contacte al equipo de desarrollo.
