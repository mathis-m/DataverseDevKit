import React, { useEffect, useRef } from 'react';
import * as d3 from 'd3';
import { ComponentResult, GroupByOption } from '../types';

interface LayerHeatmapProps {
  components: ComponentResult[];
  groupBy: GroupByOption;
  width?: number;
  height?: number;
}

export const LayerHeatmap: React.FC<LayerHeatmapProps> = ({
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

    // Prepare data
    const rowGroups = new Map<string, Map<string, number>>();
    
    components.forEach(component => {
      let rowKey: string;
      switch (groupBy) {
        case 'componentType':
          rowKey = component.componentType;
          break;
        case 'table':
          rowKey = component.tableLogicalName || 'No Table';
          break;
        case 'publisher':
          rowKey = component.publisher || 'Unknown';
          break;
        case 'managed':
          rowKey = component.isManaged ? 'Managed' : 'Unmanaged';
          break;
        default:
          rowKey = 'All';
      }

      if (!rowGroups.has(rowKey)) {
        rowGroups.set(rowKey, new Map());
      }

      component.layerSequence.forEach(solution => {
        const colMap = rowGroups.get(rowKey)!;
        colMap.set(solution, (colMap.get(solution) || 0) + 1);
      });
    });

    // Get all unique solutions
    const allSolutions = new Set<string>();
    rowGroups.forEach(colMap => {
      colMap.forEach((_, solution) => allSolutions.add(solution));
    });

    const solutions = Array.from(allSolutions).sort();
    const rows = Array.from(rowGroups.keys()).sort();

    if (rows.length === 0 || solutions.length === 0) return;

    // Create SVG
    const svg = d3.select(svgRef.current)
      .attr('width', width)
      .attr('height', height);

    const margin = { top: 60, right: 20, bottom: 100, left: 150 };
    const innerWidth = width - margin.left - margin.right;
    const innerHeight = height - margin.top - margin.bottom;

    const g = svg.append('g')
      .attr('transform', `translate(${margin.left},${margin.top})`);

    // Scales
    const xScale = d3.scaleBand()
      .domain(solutions)
      .range([0, innerWidth])
      .padding(0.05);

    const yScale = d3.scaleBand()
      .domain(rows)
      .range([0, innerHeight])
      .padding(0.05);

    const maxValue = Math.max(...Array.from(rowGroups.values()).flatMap(m => Array.from(m.values())));
    const colorScale = d3.scaleSequential(d3.interpolateBlues)
      .domain([0, maxValue]);

    // Draw cells
    rows.forEach(row => {
      const colMap = rowGroups.get(row)!;
      solutions.forEach(solution => {
        const value = colMap.get(solution) || 0;
        
        g.append('rect')
          .attr('x', xScale(solution)!)
          .attr('y', yScale(row)!)
          .attr('width', xScale.bandwidth())
          .attr('height', yScale.bandwidth())
          .attr('fill', value > 0 ? colorScale(value) : '#f5f5f5')
          .attr('stroke', '#fff')
          .attr('stroke-width', 1)
          .style('cursor', 'pointer')
          .on('mouseover', function() {
            d3.select(this).attr('opacity', 0.7);
          })
          .on('mouseout', function() {
            d3.select(this).attr('opacity', 1);
          })
          .append('title')
          .text(`${row} - ${solution}: ${value} components`);

        // Add text if value > 0
        if (value > 0) {
          g.append('text')
            .attr('x', xScale(solution)! + xScale.bandwidth() / 2)
            .attr('y', yScale(row)! + yScale.bandwidth() / 2)
            .attr('dy', '0.35em')
            .attr('text-anchor', 'middle')
            .style('font-size', '11px')
            .style('fill', value > maxValue / 2 ? '#fff' : '#000')
            .style('pointer-events', 'none')
            .text(value);
        }
      });
    });

    // X axis
    g.append('g')
      .attr('transform', `translate(0,${innerHeight})`)
      .call(d3.axisBottom(xScale))
      .selectAll('text')
      .attr('transform', 'rotate(-45)')
      .style('text-anchor', 'end')
      .style('font-size', '11px');

    // Y axis
    g.append('g')
      .call(d3.axisLeft(yScale))
      .selectAll('text')
      .style('font-size', '11px');

    // Title
    svg.append('text')
      .attr('x', width / 2)
      .attr('y', 20)
      .attr('text-anchor', 'middle')
      .style('font-size', '16px')
      .style('font-weight', 'bold')
      .text(`Layer Distribution by ${groupBy.charAt(0).toUpperCase() + groupBy.slice(1)}`);

    // Legend
    const legendWidth = 200;
    const legendHeight = 10;

    const legend = svg.append('g')
      .attr('transform', `translate(${margin.left},${height - 50})`);

    const defs = svg.append('defs');
    const linearGradient = defs.append('linearGradient')
      .attr('id', 'legend-gradient');

    linearGradient.selectAll('stop')
      .data(d3.range(0, 1.1, 0.1))
      .enter()
      .append('stop')
      .attr('offset', d => `${d * 100}%`)
      .attr('stop-color', d => colorScale(d * maxValue));

    legend.append('rect')
      .attr('width', legendWidth)
      .attr('height', legendHeight)
      .style('fill', 'url(#legend-gradient)');

    legend.append('text')
      .attr('x', 0)
      .attr('y', legendHeight + 15)
      .style('font-size', '10px')
      .text('0');

    legend.append('text')
      .attr('x', legendWidth)
      .attr('y', legendHeight + 15)
      .attr('text-anchor', 'end')
      .style('font-size', '10px')
      .text(maxValue.toString());

    legend.append('text')
      .attr('x', legendWidth / 2)
      .attr('y', -5)
      .attr('text-anchor', 'middle')
      .style('font-size', '11px')
      .text('Component Count');

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
        <strong>Layer Heatmap:</strong> Shows distribution of components across solutions. 
        Darker colors indicate more components. Helps identify which solutions have heavy layering.
      </div>
    </div>
  );
};
