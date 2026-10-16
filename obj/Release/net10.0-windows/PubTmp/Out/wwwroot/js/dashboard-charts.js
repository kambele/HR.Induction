(() => {
    const source = document.getElementById('dashboardChartData');
    if (!source) return;

    let data;
    try {
        data = JSON.parse(source.textContent || '{}');
    } catch {
        return;
    }

    const palette = {
        green: '#1b6f4a',
        greenSoft: '#6fbd8f',
        blue: '#2196f3',
        orange: '#ff9800',
        red: '#ef5350',
        purple: '#7e57c2',
        grey: '#a6adb6',
        grid: '#edf0f2',
        text: '#727985'
    };

    const showEmpty = (element, message) => {
        if (!element) return;
        element.innerHTML = '';
        const empty = document.createElement('div');
        empty.className = 'dashboard-empty-chart';
        empty.textContent = message;
        element.appendChild(empty);
    };

    const trendElement = document.getElementById('inductionTrendChart');
    let trendChart;
    if (trendElement && window.echarts) {
        trendChart = window.echarts.init(trendElement);
        trendChart.setOption({
            animationDuration: 550,
            color: [palette.blue, palette.green],
            tooltip: { trigger: 'axis', backgroundColor: '#252b36', borderWidth: 0, textStyle: { fontSize: 12 } },
            legend: { top: 12, right: 12, itemWidth: 12, itemHeight: 7, textStyle: { color: palette.text, fontSize: 11 } },
            grid: { left: 38, right: 22, top: 52, bottom: 34 },
            xAxis: {
                type: 'category',
                boundaryGap: false,
                data: (data.monthlyTrend || []).map(item => item.month),
                axisLine: { lineStyle: { color: '#dfe3e7' } },
                axisTick: { show: false },
                axisLabel: { color: palette.text, fontSize: 10 }
            },
            yAxis: {
                type: 'value',
                minInterval: 1,
                axisLine: { show: false },
                axisTick: { show: false },
                axisLabel: { color: palette.text, fontSize: 10 },
                splitLine: { lineStyle: { color: palette.grid } }
            },
            series: [
                {
                    name: 'Planned',
                    type: 'line',
                    smooth: true,
                    symbolSize: 7,
                    data: (data.monthlyTrend || []).map(item => item.planned),
                    lineStyle: { width: 2.5 },
                    areaStyle: { opacity: .08 }
                },
                {
                    name: 'Completed',
                    type: 'line',
                    smooth: true,
                    symbolSize: 7,
                    data: (data.monthlyTrend || []).map(item => item.completed),
                    lineStyle: { width: 2.5 },
                    areaStyle: { opacity: .08 }
                }
            ]
        });
    }

    const statusElement = document.getElementById('inductionStatusChart');
    let statusChart;
    const statusRows = (data.statusDistribution || []).filter(item => item.value > 0);
    if (statusElement && window.c3 && statusRows.length > 0) {
        statusChart = window.c3.generate({
            bindto: statusElement,
            size: { height: 295 },
            data: {
                columns: statusRows.map(item => [item.label, item.value]),
                type: 'donut',
                colors: {
                    'Planned': palette.grey,
                    'In progress': palette.blue,
                    'Awaiting sign-off': palette.orange,
                    'Completed': palette.green,
                    'On hold': palette.red
                }
            },
            donut: { title: 'Inductions', width: 22, label: { format: value => value } },
            legend: { position: 'bottom' },
            tooltip: { format: { value: value => `${value} record${value === 1 ? '' : 's'}` } },
            transition: { duration: 450 }
        });
    } else {
        showEmpty(statusElement, 'Status data will appear when induction records are created.');
    }

    const attendanceElement = document.getElementById('attendanceDistributionChart');
    const renderAttendance = () => {
        if (!attendanceElement || !window.d3) return;
        const rows = data.attendanceDistribution || [];
        const total = rows.reduce((sum, item) => sum + item.value, 0);
        if (total === 0) {
            showEmpty(attendanceElement, 'Attendance data will appear after employees are assigned to a programme.');
            return;
        }

        attendanceElement.innerHTML = '';
        const width = Math.max(320, attendanceElement.clientWidth || 420);
        const height = 245;
        const margin = { top: 20, right: 54, bottom: 24, left: 82 };
        const chartWidth = width - margin.left - margin.right;
        const chartHeight = height - margin.top - margin.bottom;
        const colours = { Present: palette.green, Absent: palette.red, Pending: palette.orange };
        const svg = window.d3.select(attendanceElement).append('svg')
            .attr('width', '100%')
            .attr('height', height)
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('preserveAspectRatio', 'xMidYMid meet');
        const chart = svg.append('g').attr('transform', `translate(${margin.left},${margin.top})`);
        const x = window.d3.scaleLinear().domain([0, window.d3.max(rows, item => item.value) || 1]).nice().range([0, chartWidth]);
        const y = window.d3.scaleBand().domain(rows.map(item => item.label)).range([0, chartHeight]).padding(.34);

        chart.append('g')
            .attr('transform', `translate(0,${chartHeight})`)
            .call(window.d3.axisBottom(x).ticks(5).tickSize(-chartHeight).tickFormat(window.d3.format('d')))
            .call(group => group.select('.domain').remove())
            .call(group => group.selectAll('.tick line').attr('stroke', palette.grid))
            .call(group => group.selectAll('.tick text').attr('fill', palette.text).attr('font-size', 10));
        chart.append('g')
            .call(window.d3.axisLeft(y).tickSize(0))
            .call(group => group.select('.domain').remove())
            .call(group => group.selectAll('.tick text').attr('fill', '#444b55').attr('font-size', 11).attr('font-weight', 600));

        const bars = chart.selectAll('.attendance-bar').data(rows).enter().append('g').attr('class', 'attendance-bar');
        bars.append('rect')
            .attr('x', 0)
            .attr('y', item => y(item.label))
            .attr('height', y.bandwidth())
            .attr('width', 0)
            .attr('rx', 3)
            .attr('fill', item => colours[item.label] || palette.purple)
            .transition().duration(500)
            .attr('width', item => x(item.value));
        bars.append('text')
            .attr('x', item => Math.min(chartWidth + 7, x(item.value) + 7))
            .attr('y', item => (y(item.label) || 0) + y.bandwidth() / 2 + 4)
            .attr('fill', '#4f5660')
            .attr('font-size', 11)
            .attr('font-weight', 700)
            .text(item => `${item.value} (${Math.round(item.value * 100 / total)}%)`);
    };
    renderAttendance();

    let resizeTimer;
    window.addEventListener('resize', () => {
        window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(() => {
            trendChart?.resize();
            statusChart?.resize();
            renderAttendance();
        }, 120);
    });
})();
