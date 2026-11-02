
const startButton = document.getElementById("startButton");

const configurationForm =
    document.getElementById("configurationForm");

configurationForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!configurationForm.reportValidity()) {
        return;
    }

    const minTime = Number(
        document.getElementById("minProcessingTime").value
    );

    const maxTime = Number(
        document.getElementById("maxProcessingTime").value
    );

    if (maxTime < minTime) {
        document.getElementById("errorMessage").textContent =
            "El tiempo máximo debe ser mayor o igual al mínimo.";
        return;
    }

    startButton.disabled = true;

    try {
        const response = await fetch("/Home/Start", {
            method: "POST",
            body: new FormData(configurationForm)
        });

        const result = await response.json();

        if (!response.ok) {
            throw new Error(
                result.message || "No se pudo iniciar la simulación."
            );
        }

        $("#simulationStatus")
            .removeClass("status-stopped")
            .addClass("status-running")
            .text("Ejecutándose");
        document.getElementById("errorMessage").textContent = "";
        await updateDashboard();
    } catch (error) {
        document.getElementById("errorMessage").textContent =
            error.message;
    } finally {
        await updateDashboard();
    }
});


async function updateDashboard() {
    try {
        const response = await fetch("/Home/Status", {
            cache: "no-store"
        });

        if (!response.ok) {
            throw new Error("No se pudo consultar el estado.");
        }

        const data = await response.json();

        const elapsed = Math.max(
            0,
            Math.floor(data.tiempoSimulacion || 0)
        );

        const hours = String(Math.floor(elapsed / 3600)).padStart(2, "0");
        const minutes = String(Math.floor((elapsed % 3600) / 60)).padStart(2, "0");
        const seconds = String(elapsed % 60).padStart(2, "0");

        document.getElementById("tiempoTotalSimulacion").textContent =
            `${hours}:${minutes}:${seconds}`;

        document.getElementById("simulationStatus").textContent =
            data.isRunning ? "En ejecución" : "Detenida";

        document.getElementById("queueCount").textContent =
            data.queueCount;

        document.getElementById("processingCount").textContent =
            data.processingRequests.length;

        document.getElementById("completedCount").textContent =
            data.completedRequests.length;

        document.getElementById("errorMessage").textContent =
            data.lastError || "";

        document.getElementById("timedOutCount").textContent =
            data.timedOutRequests.length;

        startButton.disabled = data.isRunning;

        renderWorkers(data.workers);
        renderWaitingRequests(data.waitingRequests);
        renderCompletedRequests(data.completedRequests);
        renderTimedOutRequests(data.timedOutRequests);
    } catch (error) {
        document.getElementById("errorMessage").textContent =
            error.message;
    }
}

function renderWorkers(workers) {
    const container = document.getElementById("workersContainer");
    container.replaceChildren();

    workers.forEach(worker => {
        const column = document.createElement("div");
        column.className = "col-md-4 mb-3";

        const card = document.createElement("div");
        card.className = "card p-3 h-100";

        const title = document.createElement("h5");
        title.textContent = worker.id;

        const status = document.createElement("p");
        status.textContent = worker.estado;

        const request = document.createElement("p");
        request.textContent = worker.idSolicitudActual
            ? `Solicitud #${worker.idSolicitudActual}`
            : "Sin solicitud asignada";

        const progress = document.createElement("div");
        progress.className = "progress";

        const bar = document.createElement("div");
        bar.className = "progress-bar";
        bar.style.width = `${worker.progreso}%`;
        bar.setAttribute("role", "progressbar");
        bar.setAttribute("aria-valuenow", worker.progreso);
        bar.setAttribute("aria-valuemin", "0");
        bar.setAttribute("aria-valuemax", "100");
        bar.textContent = `${worker.progreso}%`;

        progress.appendChild(bar);
        card.append(title, status, request, progress);
        column.appendChild(card);
        container.appendChild(column);
    });
}

function renderWaitingRequests(requests) {
    const tbody = document.getElementById("waitingTable");
    tbody.replaceChildren();

    requests.forEach(request => {
        const row = document.createElement("tr");

        const id = document.createElement("td");
        id.textContent = `#${request.id}`;

        const status = document.createElement("td");
        status.textContent = obtenerEstadoSolicitud(request.estado);

        const arrival = document.createElement("td");
        arrival.textContent = new Date(
            request.fechaLLegada
        ).toLocaleTimeString();

        row.append(id, status, arrival);
        tbody.appendChild(row);
    });
}

function renderCompletedRequests(requests) {
    const tbody = document.getElementById("completedTable");
    tbody.replaceChildren();

    requests.forEach(request => {
        const row = document.createElement("tr");

        const id = document.createElement("td");
        id.textContent = `#${request.idSolicitud}`;

        const worker = document.createElement("td");
        worker.textContent = `#${request.idProceso}`;

        const arrival = document.createElement("td");
        arrival.textContent = request.fechaLLegada
            ? new Date(request.fechaLLegada).toLocaleTimeString()
            : "-";

        const waiting = document.createElement("td");
        waiting.textContent = request.totalEsperaSegundos
            ? new Date(request.totalEsperaSegundos).toLocaleTimeString()
            : "-";

        const processing = document.createElement("td");
        processing.textContent = request.inicioAtencion
            ? new Date(request.inicioAtencion).toLocaleTimeString()
            : "-";

        /*const waiting = document.createElement("td");
        waiting.textContent =
            `${Number(request.totalEsperaSegundos || 0).toFixed(2)} s`;*/

        /*const processing = document.createElement("td");
        processing.textContent =
            `${Number(request.inicioAtencion || 0).toFixed(2)} s`;*/

        const start = document.createElement("td");
        start.textContent =
            `${Number(request.totalTiempoProcesamiento || 0).toFixed(2)} s`;

        const total = document.createElement("td");
        total.textContent =
            `${Number(request.totalTiempo || 0).toFixed(2)} s`;

        row.append(id, worker, arrival, start, waiting, processing, total);
        tbody.appendChild(row);
    });
}
function renderTimedOutRequests(requests) {
    const tbody = document.getElementById("timedOutTable");

    if (!tbody) {
        return;
    }

    tbody.replaceChildren();

    requests.forEach(request => {
        const row = document.createElement("tr");

        const id = document.createElement("td");
        id.textContent = `#${request.idSolicitud}`;

        const worker = document.createElement("td");
        worker.textContent = `#${request.idProceso}`;

        const arrival = document.createElement("td");
        arrival.textContent = request.fechaLLegada
            ? new Date(request.fechaLLegada).toLocaleTimeString()
            : "-";

        const expiration = document.createElement("td");
        expiration.textContent = request.finAtencion
            ? new Date(request.finAtencion).toLocaleTimeString()
            : "-";

        const start = document.createElement("td");
        start.textContent =
            `${Number(request.totalTiempoProcesamiento || 0).toFixed(2)} s`;

        const elapsed = document.createElement("td");
        const totalSeconds = Number(request.totalTiempo) ||
            (request.finAtencion && request.fechaLLegada
                ? (new Date(request.finAtencion) -
                    new Date(request.fechaLLegada)) / 1000
                : 0);

        elapsed.textContent = `${totalSeconds.toFixed(2)} s`;

        const reason = document.createElement("td");
        reason.textContent = request.mensajeError || "Tiempo agotado";

        row.append(id, worker, arrival, start, expiration, elapsed, reason);
        tbody.appendChild(row);
    });
}
function obtenerEstadoSolicitud(valor) {
    const estados = ["EnEspera", "Procesando", "Completada", "Fallida", "TimeOut"];
    return estados[valor] || "Desconocido";
}


// Actualiza el dashboard una vez por segundo.
updateDashboard();
setInterval(updateDashboard, 1000);
