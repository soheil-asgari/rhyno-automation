/**
 * Rhyno Contracts Dashboard ó Chart.js Charts
 *
 * Renders three charts on the Contracts Dashboard:
 *   1. Status Distribution          ñ Doughnut
 *   2. Monthly Value Trend          ñ Line
 *   3. Contract Count by Type       ñ Bar
 *
 * Respects dark mode via a MutationObserver on <html> class list.
 */
(function () {
    'use strict';

    function themeColor(light, dark) {
        return document.documentElement.classList.contains('dark') ? dark : light;
    }

    function fontOpts(size) {
        size = size || 10;
        return { family: "'Vazirmatn','Segoe UI',Tahoma,sans-serif", size: size, weight: '500' };
    }

    function gridColor() {
        return themeColor('rgba(0,0,0,0.06)', 'rgba(255,255,255,0.08)');
    }

    function tooltipBg() { return themeColor('rgba(255,255,255,0.95)', 'rgba(30,41,59,0.95)'); }
    function tooltipText() { return themeColor('#1e293b', '#f1f5f9'); }
    function tooltipBorder() { return themeColor('#e2e8f0', '#334155'); }

    var palette = [
        '#0ea5e9','#22c55e','#f59e0b','#ef4444',
        '#8b5cf6','#ec4899','#14b8a6','#f97316',
        '#6366f1','#06b6d4','#84cc16','#d946ef'
    ];

    Chart.defaults.font.family = fontOpts().family;
    Chart.defaults.font.size = 10;
    Chart.defaults.color = themeColor('#64748b', '#94a3b8');
    Chart.defaults.plugins.legend.labels.usePointStyle = true;
    Chart.defaults.plugins.legend.labels.boxWidth = 10;
    Chart.defaults.plugins.legend.labels.padding = 8;

    function parseData(canvas) {
        var raw = canvas.getAttribute('data-chart-data');
        if (!raw) return null;
        try { return JSON.parse(raw); } catch (e) { return null; }
    }

    function buildDoughnut(canvas) {
        var data = parseData(canvas);
        if (!data || !data.length) return;
        var labels = data.map(function(d){return d.label;});
        var values = data.map(function(d){return d.value;});
        var colors = data.map(function(_,i){return palette[i%palette.length];});
        new Chart(canvas.getContext('2d'), {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors.map(function(c){
                        return c+(document.documentElement.classList.contains('dark')?'cc':'99');
                    }),
                    borderColor: themeColor('#ffffff','#1e293b'),
                    borderWidth: 2,
                    hoverOffset: 6
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false, cutout: '65%',
                plugins: {
                    legend: { position: 'bottom', labels: { padding: 8, boxWidth: 10, font: fontOpts(9) } },
                    tooltip: {
                        backgroundColor: tooltipBg(), titleFont: fontOpts(10), bodyFont: fontOpts(10),
                        titleColor: tooltipText(), bodyColor: tooltipText(),
                        borderColor: tooltipBorder(), borderWidth: 1, padding: 8, cornerRadius: 6,
                        callbacks: {
                            label: function(ctx){
                                var total=ctx.dataset.data.reduce(function(a,b){return a+b;},0);
                                var pct=total>0?((ctx.parsed/total)*100).toFixed(1):'0';
                                return ctx.label+': '+ctx.formattedValue+' ('+pct+'%)';
                            }
                        }
                    }
                }
            }
        });
    }

    function buildLine(canvas) {
        var data = parseData(canvas);
        if (!data || !data.length) return;
        var labels = data.map(function(d){return d.label;});
        var values = data.map(function(d){return d.value;});
        new Chart(canvas.getContext('2d'), {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: '«—“‘ ò· ( Ê„«‰)',
                    data: values,
                    fill: true,
                    backgroundColor: function(ctx){
                        var g=ctx.chart.ctx.createLinearGradient(0,0,0,ctx.chart.height);
                        g.addColorStop(0,themeColor('rgba(14,165,233,0.30)','rgba(14,165,233,0.25)'));
                        g.addColorStop(1,themeColor('rgba(14,165,233,0.02)','rgba(14,165,233,0.02)'));
                        return g;
                    },
                    borderColor: themeColor('#0ea5e9','#38bdf8'),
                    borderWidth: 2,
                    pointBackgroundColor: themeColor('#0ea5e9','#38bdf8'),
                    pointBorderColor: themeColor('#ffffff','#1e293b'),
                    pointBorderWidth: 1.5,
                    pointRadius: 3, pointHoverRadius: 5, tension: 0.3
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: tooltipBg(), titleFont: fontOpts(10), bodyFont: fontOpts(10),
                        titleColor: tooltipText(), bodyColor: tooltipText(),
                        borderColor: tooltipBorder(), borderWidth: 1, padding: 8, cornerRadius: 6,
                        callbacks: { label: function(ctx){ return Number(ctx.parsed.y).toLocaleString('fa-IR')+'  Ê„«‰'; } }
                    }
                },
                scales: {
                    x: { grid: { color: gridColor(), drawBorder: false }, ticks: { font: fontOpts(9), maxRotation: 45, minRotation: 0 } },
                    y: { grid: { color: gridColor(), drawBorder: false }, ticks: { font: fontOpts(9), callback: function(v){ return Number(v).toLocaleString('fa-IR'); } } }
                }
            }
        });
    }

    function buildBar(canvas) {
        var data = parseData(canvas);
        if (!data || !data.length) return;
        var labels = data.map(function(d){return d.label;});
        var values = data.map(function(d){return d.value;});
        var colors = data.map(function(_,i){return palette[i%palette.length];});
        new Chart(canvas.getContext('2d'), {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: ' ⁄œ«œ ﬁ—«—œ«œ',
                    data: values,
                    backgroundColor: colors.map(function(c){ return c+(document.documentElement.classList.contains('dark')?'bb':'88'); }),
                    borderColor: colors, borderWidth: 1.5, borderRadius: 4, maxBarThickness: 48
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false, indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: tooltipBg(), titleFont: fontOpts(10), bodyFont: fontOpts(10),
                        titleColor: tooltipText(), bodyColor: tooltipText(),
                        borderColor: tooltipBorder(), borderWidth: 1, padding: 8, cornerRadius: 6
                    }
                },
                scales: {
                    x: { grid: { color: gridColor(), drawBorder: false }, ticks: { font: fontOpts(9), stepSize: 1, callback: function(v){ return Number.isInteger(v)?v:''; } } },
                    y: { grid: { display: false, drawBorder: false }, ticks: { font: fontOpts(9) } }
                }
            }
        });
    }

    function initCharts() {
        if (typeof Chart === 'undefined') { setTimeout(initCharts, 200); return; }
        var canvases = document.querySelectorAll('canvas[data-chart-type]');
        if (!canvases.length) return;
        canvases.forEach(function(canvas){
            var type = canvas.getAttribute('data-chart-type');
            if (type === 'doughnut') buildDoughnut(canvas);
            else if (type === 'line') buildLine(canvas);
            else if (type === 'bar') buildBar(canvas);
        });
    }

    var reInitTimer = null;
    var observer = new MutationObserver(function(){
        clearTimeout(reInitTimer);
        reInitTimer = setTimeout(function(){
            var charts = Chart.instances;
            if (charts) { Object.keys(charts).forEach(function(k){ var c=charts[k]; if(c&&typeof c.destroy==='function') c.destroy(); }); }
            initCharts();
        }, 300);
    });
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initCharts);
    else initCharts();
})();
