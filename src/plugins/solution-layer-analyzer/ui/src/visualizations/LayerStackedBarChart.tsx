import React, { useEffect, useRef } from 'react';
import * as d3 from 'd3';
import { ComponentResult, GroupByOption } from '../types';

interface LayerStackedBarChartProps {
  components: ComponentResult[];
  groupBy: GroupByOption;
  width?: number;
  height?: number;
}

export const LayerStackedBarChart: React.FC<LayerStackedBarChartProps> = ({
  components,
  groupBy,
  width = 800,
  height = 600,
}) => {
  const svgRef = useRef<SVGSVGElement>(null);

  useEffect(() => {
    if (!svgRef.current || components.length === 0) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    // Prepare data: count layers per group
    const groupData = new Map<string, Map<number, number>>();
    
    components.forEach(component => {
      let groupKey: string;
      switch (groupBy) {
        case 'componentType':
          groupKey = component.componentType;
          break;
        case 'table':
          groupKey = component.tableLogicalName || 'No Table';
          break;
        case 'publisher':
          groupKey = component.publisher || 'Unknown';
          break;
        case 'solution':
          groupKey = component.layerSequence[0] || 'Unknown';
          break;
        case 'managed':
          groupKey = component.isManaged ? 'Managed' : 'Unmanaged';
          break;
        default:
          groupKey = 'All';
      }

      if (!groupData.has(groupKey)) {
        groupData.set(groupKey, new Map());
      }

      const layerCount = component.layerSequence.length;
      const layerMap = groupData.get(groupKey)!;
      layerMap.set(layerCount, (layerMap.get(layerCount) || 0) + 1);
    });

    // Convert to array format for D3
    const maxLayers = Math.max(...components.map(c => c.layerSequence.length));
    const layerKeys = Array.from({ length: maxLayers }, (_, i) => i + 1);
    
    const data = Array.from(groupData.entries()).map(([group, layerMap]) => {
      const obj: any = { group };
      layerKeys.forEach(layerCount => {
        obj[layerCount] = layerMap.get(layerCount) || 0;
      });
      return obj;
    }).sort((a, b) => {
      const aTotal = layerKeys.reduce((sum, k) => sum + a[k], 0);
      const bTotal = layerKeys.reduce((sum, k) => sum + b[k], 0);
      return bTotal - aTotal;
    });

    if (data.length === 0) return;

    // Create SVG
    const svg = d3.select(svgRef.current)
      .attr('width', width)
      .attr('height', height);

    const margin = { top: 60, right: 120, bottom: 80, left: 150 };
    const innerWidth = width - margin.left - margin.right;
    const innerHeight = height - margin.top - margin.bottom;

    const g = svg.append('g')
      .attr('transform', `translate(${margin.left},${margin.top})`);

    // Scales
    const xScale = d3.scaleBand()
      .domain(data.map(d => d.group))
      .range([0, innerWidth])
      .padding(0.2);

    const yScale = d3.scaleLinear()
      .domain([0, d3.max(data, d => layerKeys.reduce((sum, k) => sum + d[k], 0)) || 0])
      .range([innerHeight, 0])
      .nice();

    const colorScale = d3.scaleOrdinal()
      .domain(layerKeys.map(String))
      .range(d3.schemeSpectral[Math.max(3, Math.min(11, maxLayers))]);

    // Stack data
    const stack = d3.stack()
      .keys(layerKeys.map(String))
      .value((d: any, key) => d[key] || 0);

    const series = stack(data);

    // Draw bars
    const bars = g.selectAll('.series')
      .data(series)
      .enter()
      .append('g')
      .attr('class', 'series')
      .attr('fill', d => colorScale(d.key) as string);

    bars.selectAll('rect')
      .data(d => d)
      .enter()
      .append('rect')
      .attr('x', (d: any) => xScale(d.data.group)!)
      .attr('y', d => yScale(d[1]))
      .attr('height', d => yScale(d[0]) - yScale(d[1]))
      .attr('width', xScale.bandwidth())
      .style('cursor', 'pointer')
      .on('mouseover', function() {
        d3.select(this).attr('opacity', 0.7);
      })
      .on('mouseout', function() {
        d3.select(this).attr('opacity', 1);
      })
      .append('title')
      .text((d: any, i, nodes) => {
        const parentElement = nodes[i].parentNode as Element | null;
        if (!parentElement) return '';
        const key = d3.select(parentElement as any).datum() as any;
        const value = d[1] - d[0];
        return `${d.data.group}\n${key.key} layer${key.key > 1 ? 's' : ''}: ${value} components`;
      });

    // X axis
    g.append('g')
      .attr('transform', `translate(0,${innerHeight})`)
      .call(d3.axisBottom(xScale))
      .selectAll('text')
      .attr('transform', 'rotate(-45)')
      .style('text-anchor', 'end')
      .style('font-size', '10px');

    // Y axis
    g.append('g')
      .call(d3.axisLeft(yScale).ticks(5))
      .append('text')
      .attr('transform', 'rotate(-90)')
      .attr('y', -60)
      .attr('x', -innerHeight / 2)
      .attr('text-anchor', 'middle')
      .style('font-size', '12px')
      .style('fill', '#000')
      .text('Number of Components');

    // Title
    svg.append('text')
      .attr('x', width / 2)
      .attr('y', 20)
      .attr('text-anchor', 'middle')
      .style('font-size', '16px')
      .style('font-weight', 'bold')
      .text(`Layer Depth Distribution by ${groupBy.charAt(0).toUpperCase() + groupBy.slice(1)}`);

    // Legend
    const legend = svg.append('g')
      .attr('transform', `translate(${width - margin.right + 10},${margin.top})`);

    legend.append('text')
      .attr('x', 0)
      .attr('y', -10)
      .style('font-size', '12px')
      .style('font-weight', 'bold')
      .text('Layer Count');

    layerKeys.forEach((layerCount, i) => {
      const legendRow = legend.append('g')
        .attr('transform', `translate(0,${i * 20})`);

      legendRow.append('rect')
        .attr('width', 15)
        .attr('height', 15)
        .attr('fill', colorScale(String(layerCount)) as string);

      legendRow.append('text')
        .attr('x', 20)
        .attr('y', 12)
        .style('font-size', '11px')
        .text(`${layerCount} layer${layerCount > 1 ? 's' : ''}`);
    });

  }, [components, groupBy, width, height]);

  if (components.length === 0) {
    return (
      <div style={{ textAlign: 'center', padding: '40px', color: '#666' }}>
        No data to visualize. Index solutions first.
      </div>
    );
  }

  return (
    <div>
      <svg ref={svgRef} style={{ border: '1px solid #ddd', borderRadius: '4px' }} />
      <div style={{ marginTop: '10px', fontSize: '12px', color: '#666' }}>
        <strong>Layer Depth Chart:</strong> Shows how many layers components have, grouped by {groupBy}. 
        Different colors represent different layer counts. Helps identify over-layered components.
      </div>
    </div>
  );
};
