// Cuenta atrás de la ronda.
//
// El servidor manda el instante de fin en UTC y su propia hora; el navegador calcula una sola vez
// la diferencia entre los dos relojes y a partir de ahí cuenta solo. Así:
//  - recargar la página no reinicia nada, porque el fin es un instante, no unos segundos restantes;
//  - un portátil con la hora mal puesta sigue viendo el tiempo correcto;
//  - y el cronómetro no gasta ni un mensaje del circuito por segundo.

const timers = new WeakMap();

export function start(element, options) {
    stop(element);

    const skew = options.serverNowMs - Date.now();

    const render = () => {
        const remaining = options.endsAtMs - (Date.now() + skew);

        if (remaining > 0) {
            element.textContent = format(remaining);
            element.dataset.state = 'running';
            return;
        }

        // Pasado el tiempo normal viene la prórroga, si el juego la tiene.
        const extra = options.extraEndsAtMs - (Date.now() + skew);

        if (extra > 0) {
            element.textContent = `${options.extraLabel} ${format(extra)}`;
            element.dataset.state = 'extra';
            return;
        }

        element.textContent = options.overLabel;
        element.dataset.state = 'over';
        stop(element);
    };

    render();
    timers.set(element, setInterval(render, 1000));
}

export function stop(element) {
    const timer = timers.get(element);

    if (timer) {
        clearInterval(timer);
        timers.delete(element);
    }
}

function format(milliseconds) {
    const total = Math.floor(milliseconds / 1000);
    const minutes = Math.floor(total / 60);
    const seconds = total % 60;

    return `${minutes}:${String(seconds).padStart(2, '0')}`;
}
