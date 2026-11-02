using ServidorWeb.Models;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace ServidorWeb.Servicios
{
    public class SimulacionService
    {
        private SemaphoreSlim _semaphore;

        private readonly ConcurrentQueue<Solicitud> ColaSolicitud = new ConcurrentQueue<Solicitud>();

        private readonly List<Proceso> procesos = new List<Proceso>();
        private readonly List<Solicitud> solicitudes = new();
        public bool Ejecutandose {  get; private set; }
        public string? MensajeError { get; private set; }
        private readonly object _estadoBloqueo = new();
        private ServerConfig? ServerConfig;
        public DateTime? InicioSimulacion { get; private set; }
        public DateTime? FinSimulacion { get; private set; }
        public double TiempoDeSimulacion
        {
            get
            {
                if (!InicioSimulacion.HasValue)
                    return 0;

                DateTime total = FinSimulacion ?? DateTime.Now;

                return (total - InicioSimulacion.Value).TotalSeconds;
            }
        }

        public void Configuracion(ServerConfig serverConfig)
        {
            if (serverConfig.MaxTrabajadores < 1)
                throw new ArgumentException("Debe de configurar al menos un trabajador.");

            if(serverConfig.TiempoMinimoProcesamientoMs < 1 
                || serverConfig.TiempoMaximoProcesamientoMs < 1 
                || serverConfig.TiempoMaximoEspera < 1)
            {
                throw new ArgumentException("Los tiempos de la configuración no son válidos.");
            }


            ServerConfig = serverConfig;
            //Liberación del semaforo en memoria
            _semaphore?.Dispose();

            //Creación del semaforo con N cantidad de procesos(trabajos)
            _semaphore = new SemaphoreSlim(serverConfig.MaxTrabajadores, serverConfig.MaxTrabajadores);

            //Limpiando cola y procesos
            ColaSolicitud.Clear();
            procesos.Clear();
            solicitudes.Clear();

            for (int i = 1; i <= serverConfig.MaxTrabajadores; i++)
            {
                procesos.Add(new Proceso
                {
                    Id = $"Trabajador {i}",
                    Estado = EstadoProceso.Disponible,
                    Ocupado = false,
                    IdSolicitudActual = null,
                    Progreso = 0
                });
            }
        }

        

        public void AgregarSolicitud(Solicitud solicitud)
        {
            solicitud.Estado = EstadoSolicitud.EnEspera;
            solicitud.Progreso = 0;

            solicitudes.Add(solicitud);
            ColaSolicitud.Enqueue(solicitud);
        }

        public List<Solicitud> ObtenerCola()
        {
            return ColaSolicitud.ToList();
        }

        public List<Solicitud> ObtenerSolicitudes()
        {
            return solicitudes;
        }

        public int LongitudCola()
        {
            return ColaSolicitud.Count;
        }

        public List<Proceso> ListaProcesos()
        {
            return procesos;
        }

        public async Task IniciarSimulacionAsync()
        {
            InicioSimulacion = DateTime.Now;
            FinSimulacion = null;

            using var monitorCts = new CancellationTokenSource();

            try
            {
                var monitorSolicitudes = MonitorColaTimeOutAsync(monitorCts.Token);

                var solicitudesProcesos = procesos.Select(x => ProcesarColaAsync(x)).ToArray();

                await Task.WhenAll(solicitudesProcesos);
                monitorCts.Cancel();

                await monitorSolicitudes;
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                MensajeError = ex.Message;
            }
            finally
            {
                Ejecutandose = false;
            }
        }

        private async Task MonitorColaTimeOutAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(200, token);

                lock (_estadoBloqueo)
                {
                    foreach (var solicitud in solicitudes)
                    {
                        if(solicitud.Estado != EstadoSolicitud.EnEspera)
                        {
                            continue;
                        }

                        var tiempoTranscurrido = DateTime.Now - solicitud.FechaLLegada;

                        if(tiempoTranscurrido.TotalSeconds >= ServerConfig.TiempoMaximoEspera)
                        {
                            solicitud.Estado = EstadoSolicitud.TimeOut;
                            solicitud.FinAtencion = DateTime.Now;
                            solicitud.MensajeError = "Se agotó el tiempo máximo de espera configurado.";
                            solicitud.totalTiempo = (solicitud.FinAtencion.Value - solicitud.FechaLLegada).TotalSeconds;
                        }
                    }
                }
            }
        }

        public bool IniciarSimulacion()
        {
            if (_semaphore == null) throw new InvalidOperationException("Primero debe de configurar la simulación");

            if (Ejecutandose)
            {
                MensajeError = "La simulación ya está en ejecución";
                return false;
            }

            Ejecutandose = true;
            MensajeError = null;

            _ = IniciarSimulacionAsync();

            return true;
        }

        private async Task ProcesarColaAsync(Proceso proceso)
        {
            while (ColaSolicitud.TryDequeue(out var solicitud))
            {
                if(solicitud.Estado != EstadoSolicitud.EnEspera)
                {
                    continue;
                }

                await ProcesarSolicitudAsync(solicitud, proceso);
            }
        }

        public async Task ProcesarSolicitudAsync(Solicitud solicitud, Proceso proceso)
        {
            if (_semaphore == null)
                throw new InvalidOperationException("Servicio no configurado, configure el servicio antes.");

            await _semaphore!.WaitAsync();

            try
            {
                TimeSpan tiempoEsperando;

                lock(_estadoBloqueo)
                {
                    if(solicitud.Estado != EstadoSolicitud.EnEspera)
                    {
                        return;
                    }

                    var tiempoTranscurrido = DateTime.Now - solicitud.FechaLLegada;

                    tiempoEsperando = TimeSpan.FromSeconds(ServerConfig.TiempoMaximoEspera) - tiempoTranscurrido;

                    if(tiempoEsperando <= TimeSpan.Zero)
                    {
                        solicitud.Estado = EstadoSolicitud.TimeOut;
                        solicitud.FinAtencion = DateTime.Now;
                        solicitud.MensajeError = "Se agotó el tiempo máximo en la cola.";

                        return;
                    }

                    solicitud.Estado = EstadoSolicitud.Procesando;
                    solicitud.InicioAtencion = DateTime.Now;
                    solicitud.IdProceso = proceso.Id;

                    proceso.Ocupado = true;
                    proceso.IdSolicitudActual = solicitud.Id;
                    proceso.Estado = EstadoProceso.Procesando;
                    proceso.Progreso = 0;
                }

                using var timeoutCts = new CancellationTokenSource();

                timeoutCts.CancelAfter(tiempoEsperando);

                int totalSeconds = solicitud.TiempoProcesoSegundos;

                for(int segundos = 1; segundos <= totalSeconds; segundos++)
                {
                    var tiempoEsperaProceso = tiempoEsperando - TimeSpan.FromSeconds(segundos - 1);

                    if(tiempoEsperaProceso  <= TimeSpan.Zero)
                    {
                        timeoutCts.Cancel();
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), timeoutCts.Token);

                    int progreso = (segundos * 100) / totalSeconds;

                    solicitud.Progreso = progreso;
                    proceso.Progreso = progreso;
                }

                solicitud.FinAtencion = DateTime.Now;
                solicitud.Progreso = 100;
                solicitud.Estado = EstadoSolicitud.Completada;
                solicitud.MensajeError = null;
                solicitud.totalEsperaSegundos = (solicitud.InicioAtencion.Value - solicitud.FechaLLegada).TotalSeconds;
                solicitud.totalTiempoProcesamiento = (solicitud.FinAtencion.Value - solicitud.InicioAtencion!.Value).TotalSeconds;
                solicitud.totalTiempo = (solicitud.FinAtencion.Value - solicitud.FechaLLegada).TotalSeconds;
            }
            catch (OperationCanceledException)
            {
                solicitud.FinAtencion = DateTime.Now;
                solicitud.Estado = EstadoSolicitud.TimeOut;
                solicitud.MensajeError = "Se agotó el tiempo máximo de espera en cola.";
                solicitud.totalEsperaSegundos = (solicitud.InicioAtencion!.Value - solicitud.FechaLLegada).TotalSeconds;
                solicitud.totalTiempoProcesamiento = (solicitud.FinAtencion!.Value - solicitud.InicioAtencion!.Value).TotalSeconds;
                solicitud.totalTiempo = (solicitud.FinAtencion.Value - solicitud.FechaLLegada).TotalSeconds;
            }
            finally
            {
                if (solicitud.InicioAtencion.HasValue)
                {
                    solicitud.totalEsperaSegundos = (solicitud.InicioAtencion.Value - solicitud.FechaLLegada).TotalSeconds;
                }

                proceso.Ocupado = false;
                proceso.Estado = EstadoProceso.Disponible;
                proceso.IdSolicitudActual = null;
                proceso.Progreso = 0;

                FinSimulacion = DateTime.Now;

                _semaphore.Release();
            }
        }

        public int ObtenerTamanioCola()
        {
            return ColaSolicitud.Count;
        }

        public List<Proceso> ObtenerProcesos()
        {
            return procesos.ToList();
        }
    } 
}
