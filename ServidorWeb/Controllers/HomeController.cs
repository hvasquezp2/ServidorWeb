using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServidorWeb.Models;
using ServidorWeb.Servicios;

namespace ServidorWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SimulacionService _simulacionService;

        public HomeController(ILogger<HomeController> logger, SimulacionService simulacionService)
        {
            _logger = logger;
            _simulacionService= simulacionService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        /*
        public async Task<IActionResult> TestSimulacion()
        {
            var configuracion = new ServerConfig
            {
                CapacidadSolicitudes = 20,
                MaxTrabajadores = 5,
                TiempoMaximoEspera = 15,
                TiempoMaximoProcesamientoMs = 8,
                TiempoMinimoProcesamientoMs = 5
            };

            //Configuración inicial y creación de procesos
            _simulacionService.Configuracion(configuracion);

            var solicitudes = new List<Solicitud>();

            for (int i = 1; i <= configuracion.CapacidadSolicitudes; i++) {
                var solicitud = new Solicitud
                {
                    IdSolicitud = "",
                    FechaLLegada = DateTime.Now,
                    TiempoProcesoSegundos = configuracion.TiempoMaximoProcesamientoMs,
                    Estado = EstadoSolicitud.EnEspera
                };

                _simulacionService.AgregarSolicitud(solicitud);
                solicitudes.Add(solicitud);
            }

            var fechaInicio = DateTime.Now;

            await _simulacionService.IniciarSimulacionAsync();

            var tiempoTranscurrido = (DateTime.Now - fechaInicio).TotalSeconds;

            var results = _simulacionService.ObtenerSolicitudes();

            var mensaje = string.Join(
                Environment.NewLine, 
                results.Select(x => $"Solicitud: {x.IdSolicitud} - Estado: {x.Estado} - Trabajador: {x.IdProceso} - Tiempo de espera: {x.totalEsperaAtencion:F2} Segundos"));
            
            return Content(
                $"Tiempo Total: {tiempoTranscurrido:F2} segundos." +
                Environment.NewLine +
                $"Solicitudes procesadas: {results.Count} " +
                Environment.NewLine + 
                Environment.NewLine +
                mensaje);
        } */

        [HttpPost]
        public IActionResult Start([FromForm] ServerConfig configuration)
        {
            if (_simulacionService.Ejecutandose)
            {
                return Conflict(new
                {
                    message = "Ya hay una simulación en ejecución."
                });
            }

            if (configuration.MaxTrabajadores < 1 ||
                configuration.MaxTrabajadores > 20 ||
                configuration.CapacidadSolicitudes < 1 ||
                configuration.CapacidadSolicitudes > 500 ||
                configuration.TiempoMinimoProcesamientoMs < 1 ||
                configuration.TiempoMaximoProcesamientoMs <
                    configuration.TiempoMinimoProcesamientoMs ||
                configuration.TiempoMaximoProcesamientoMs > 300 ||
                configuration.TiempoMaximoEspera < 1 ||
                configuration.TiempoMaximoEspera > 300)
            {
                return BadRequest(new
                {
                    message = "La configuración contiene valores inválidos."
                });
            }

            _simulacionService.Configuracion(configuration);


            var random = new Random();

            for (int i = 1; i <= configuration.CapacidadSolicitudes; i++)
            {
                var request = new Solicitud
                {
                    Id = i,
                    IdSolicitud = $"Solicitud {i}",
                    FechaLLegada = DateTime.Now,
                    TiempoProcesoSegundos = random.Next(
                        configuration.TiempoMinimoProcesamientoMs,
                        configuration.TiempoMaximoProcesamientoMs + 1),
                    Estado = EstadoSolicitud.EnEspera
                };

                _simulacionService.AgregarSolicitud(request);
            }

            bool started = _simulacionService.IniciarSimulacion();

            if (!started)
            {
                return Conflict(new
                {
                    message = "No fue posible iniciar la simulación."
                });
            }

            return Ok(new
            {
                message = "Simulación iniciada correctamente."
            });
        }


        [HttpGet]
        public IActionResult Status()
        {
            var requests = _simulacionService.ObtenerSolicitudes();

            var timedOutRequestsResult = requests
                .Where(r => r.Estado == EstadoSolicitud.TimeOut)
                .ToList();

            return Json(new
            {
                isRunning = _simulacionService.Ejecutandose,
                lastError = _simulacionService.MensajeError,
                queueCount = _simulacionService.ObtenerTamanioCola(),

                workers = _simulacionService.ObtenerProcesos(),

                waitingRequests = requests
                    .Where(r => r.Estado == EstadoSolicitud.EnEspera),

                processingRequests = requests
                    .Where(r => r.Estado == EstadoSolicitud.Procesando),

                completedRequests = requests
                    .Where(r => r.Estado == EstadoSolicitud.Completada),

                tiempoSimulacion = _simulacionService.TiempoDeSimulacion,

                timedOutRequests = timedOutRequestsResult
            });
        }
    }
}
