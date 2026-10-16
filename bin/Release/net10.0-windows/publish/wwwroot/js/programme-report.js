(() => {
    const parse = id => JSON.parse(document.getElementById(id)?.textContent || '[]');
    const empty = (element, message) => {
        element.innerHTML = '';
        const note = document.createElement('div');
        note.className = 'chart-empty';
        note.textContent = message;
        element.append(note);
    };

    const divisionData = parse('divisionRatingsData');
    const divisionElement = document.getElementById('divisionRatingsChart');
    let divisionChart;
    if (divisionElement && divisionData.some(item => item.responses > 0)) {
        divisionChart = echarts.init(divisionElement);
        divisionChart.setOption({
            color: ['#2e7b4f'],
            grid: { left: 28, right: 35, top: 18, bottom: 70, containLabel: true },
            tooltip: { trigger: 'axis', formatter: params => `${params[0].name}<br>${params[0].value.toFixed(2)} / 5` },
            xAxis: { type: 'category', data: divisionData.map(item => item.division), axisLabel: { interval: 0, rotate: 28, color: '#66736c' }, axisLine: { lineStyle: { color: '#dce4dd' } } },
            yAxis: { type: 'value', min: 0, max: 5, interval: 1, axisLabel: { color: '#66736c' }, splitLine: { lineStyle: { color: '#edf1ed' } } },
            series: [{ type: 'bar', barMaxWidth: 42, data: divisionData.map(item => ({ value: item.average, itemStyle: { borderRadius: [4, 4, 0, 0] } })), label: { show: true, position: 'top', formatter: value => Number(value.value).toFixed(1), color: '#314037', fontWeight: 700 }]
        });
    } else if (divisionElement) empty(divisionElement, 'No presentation ratings have been submitted.');

    const overallData = parse('overallRatingData');
    const overallElement = document.getElementById('overallRatingChart');
    let overallChart;
    if (overallElement && overallData.some(item => item.count > 0)) {
        overallChart = c3.generate({
            bindto: overallElement,
            size: { height: 330 },
            data: { columns: overallData.map(item => [`${item.rating} - ${['','Poor','Fair','Good','Very good','Excellent'][item.rating]}`, item.count]), type: 'donut', order: null },
            donut: { title: 'Responses', width: 28, label: { format: value => value } },
            color: { pattern: ['#b42318','#d28b2c','#607d8b','#2e7b4f','#91cf49'] },
            legend: { position: 'bottom' },
            tooltip: { format: { value: value => `${value} response${value === 1 ? '' : 's'}` } }
        });
    } else if (overallElement) empty(overallElement, 'No overall programme ratings have been submitted.');

    const timeData = parse('timeAllocationData');
    const timeElement = document.getElementById('timeAllocationChart');
    if (timeElement && timeData.some(item => item.count > 0)) {
        const total = d3.sum(timeData, item => item.count);
        const width = Math.max(320, timeElement.clientWidth - 32);
        const height = 190;
        const colors = d3.scaleOrdinal().domain(timeData.map(item => item.label)).range(['#2e7b4f','#d28b2c','#607d8b']);
        const scale = d3.scaleLinear().domain([0, total]).range([0, width]);
        const svg = d3.select(timeElement).append('svg').attr('viewBox', `0 0 ${width} ${height}`).attr('role', 'img').attr('aria-label', 'Time allocation response distribution');
        let cursor = 0;
        timeData.forEach(item => {
            const segmentWidth = scale(item.count);
            svg.append('rect').attr('x', cursor).attr('y', 30).attr('width', segmentWidth).attr('height', 54).attr('fill', colors(item.label));
            if (segmentWidth > 44) svg.append('text').attr('class', 'd3-allocation-value').attr('x', cursor + segmentWidth / 2).attr('y', 62).attr('text-anchor', 'middle').text(item.count);
            cursor += segmentWidth;
        });
        timeData.forEach((item, index) => {
            svg.append('circle').attr('cx', 10).attr('cy', 112 + index * 24).attr('r', 6).attr('fill', colors(item.label));
            svg.append('text').attr('class', 'd3-allocation-label').attr('x', 24).attr('y', 116 + index * 24).text(`${item.label}: ${item.count} (${Math.round(item.count * 100 / total)}%)`);
        });
    } else if (timeElement) empty(timeElement, 'No time-allocation feedback has been submitted.');

    window.addEventListener('resize', () => { divisionChart?.resize(); overallChart?.resize(); });
})();
