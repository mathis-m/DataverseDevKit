import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { UpSetPlotData, IntersectionData } from '../types/analytics';
import { createTooltip, showTooltip, hideTooltip } from '../utils/d3-helpers';
import { Button } from '@fluentui/react-components';
import { ZoomIn24Regular, ZoomOut24Regular, ArrowReset24Regular } from '@fluentui/react-icons';

interface UpSetPlotProps {
  data: UpSetPlotData;
  width?: number;
  height?: number;
  onIntersectionClick?: (intersection: IntersectionData) => void;
}

type SortOption = 'size' | 'degree';

export const UpSetPlot: React.FC<UpSetPlotProps> = ({
  data,
  width = 1000,
  height = 600,
  onIntersectionClick
}) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const [transform, setTransform] = useState<d3.ZoomTransform>(d3.zoomIdentity);
  const [sortBy, setSortBy] = useState<SortOption>('size');
  const [selectedIntersection, setSelectedIntersection] = useState<number | null>(null);

  useEffect(() => {
    if (!svgRef.current || !data.solutions.length) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    const svg = d3.select(svgRef.current);
    const g = svg.append('g').attr('class', 'main-group');

    // Create tooltip
    const tooltip = createTooltip('upset-plot-tooltip');

    // Setup zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 5])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
        setTransform(event.transform);
      });

    svg.call(zoom);

    // Dimensions
    const margin = { top: 50, right: 40, bottom: 150, left: 200 };
    const matrixWidth = data.intersections.length * 30;
    const matrixHeight = data.solutions.length * 30;
    const barHeight = 200;
    const gap = 20;

    // Sort intersections
    const sortedIntersections = [...data.intersections].sort((a, b) => {
      if (sortBy === 'size') return b.size - a.size;
      return b.degree - a.degree;
    });

    // Scales
    const xScale = d3.scaleBand()
      .domain(sortedIntersections.map((_, i) => i.toString()))
      .range([0, matrixWidth])
      .padding(0.1);

    const yScale = d3.scaleBand()
      .domain(data.solutions)
      .range([0, matrixHeight])
      .padding(0.1);

    const barScale = d3.scaleLinear()
      .domain([0, d3.max(sortedIntersections, d => d.size) || 0])
      .range([0, barHeight]);

    // Color scale
    const colorScale = d3.scaleSequential(d3.interpolateBlues)
      .domain([0, d3.max(sortedIntersections, d => d.size) || 0]);

    // Position the main group
    g.attr('transform', `translate(${margin.left},${margin.top})`);

    // Draw bar chart (top)
    const barGroup = g.append('g')
      .attr('class', 'bar-chart')
      .attr('transform', `translate(0,${-gap - barHeight})`);

    barGroup.selectAll('rect')
      .data(sortedIntersections)
      .join('rect')
      .attr('x', (d, i) => xScale(i.toString())!)
      .attr('y', d => barHeight - barScale(d.size))
      .attr('width', xScale.bandwidth())
      .attr('height', d => barScale(d.size))
      .attr('fill', d => colorScale(d.size))
      .attr('stroke', (d, i) => i === selectedIntersection ? '#000' : 'none')
      .attr('stroke-width', 3)
      .style('cursor', 'pointer')
      .on('mouseover', function(event, d) {
        d3.select(this).attr('opacity', 0.7);
        const content = `
          <strong>Intersection</strong><br/>
          Size: ${d.size} components<br/>
          Degree: ${d.degree} solutions<br/>
          Solutions: ${d.solutions.join(', ')}<br/>
          <br/>
          <strong>Top Components:</strong><br/>
          ${d.componentNames.slice(0, 5).join('<br/>')}
          ${d.componentNames.length > 5 ? `<br/>... and ${d.componentNames.length - 5} more` : ''}
        `;
        showTooltip(tooltip, content, event);
      })
      .on('mouseout', function() {
        d3.select(this).attr('opacity', 1);
        hideTooltip(tooltip);
      })
      .on('click', function(event, d) {
        event.stopPropagation();
        const index = sortedIntersections.indexOf(d);
        setSelectedIntersection(index);
        if (onIntersectionClick) {
          onIntersectionClick(d);
        }
      });

    // Add bar labels
    barGroup.selectAll('text')
      .data(sortedIntersections)
      .join('text')
      .attr('x', (d, i) => xScale(i.toString())! + xScale.bandwidth() / 2)
      .attr('y', d => barHeight - barScale(d.size) - 5)
      .attr('text-anchor', 'middle')
      .attr('font-size', '10px')
      .attr('font-weight', 'bold')
      .attr('fill', '#333')
      .text(d => d.size);

    // Draw matrix (bottom)
    const matrixGroup = g.append('g')
      .attr('class', 'matrix');

    // Draw connecting lines
    sortedIntersections.forEach((intersection, i) => {
      const activeSolutions = intersection.solutions;
      const positions = activeSolutions.map(s => ({
        solution: s,
        y: yScale(s)! + yScale.bandwidth() / 2
      }));

      if (positions.length > 1) {
        const x = xScale(i.toString())! + xScale.bandwidth() / 2;
        const minY = Math.min(...positions.map(p => p.y));
        const maxY = Math.max(...positions.map(p => p.y));

        matrixGroup.append('line')
          .attr('x1', x)
          .attr('y1', minY)
          .attr('x2', x)
          .attr('y2', maxY)
          .attr('stroke', i === selectedIntersection ? '#0078D4' : '#999')
          .attr('stroke-width', i === selectedIntersection ? 3 : 2)
          .style('pointer-events', 'none');
      }
    });

    // Draw dots
    sortedIntersections.forEach((intersection, i) => {
      data.solutions.forEach(solution => {
        const isActive = intersection.solutions.includes(solution);
        
        matrixGroup.append('circle')
          .attr('cx', xScale(i.toString())! + xScale.bandwidth() / 2)
          .attr('cy', yScale(solution)! + yScale.bandwidth() / 2)
          .attr('r', isActive ? 6 : 3)
          .attr('fill', isActive ? (i === selectedIntersection ? '#0078D4' : '#333') : '#ddd')
          .attr('stroke', isActive && i === selectedIntersection ? '#000' : 'none')
          .attr('stroke-width', 2)
          .style('pointer-events', 'none');
      });
    });

    // Draw solution labels (left)
    const labelGroup = g.append('g')
      .attr('class', 'solution-labels')
      .attr('transform', `translate(-10,0)`);

    labelGroup.selectAll('text')
      .data(data.solutions)
      .join('text')
      .attr('x', 0)
      .attr('y', d => yScale(d)! + yScale.bandwidth() / 2)
      .attr('dy', '.35em')
      .attr('text-anchor', 'end')
      .attr('font-size', '12px')
      .attr('font-weight', 'bold')
      .attr('fill', '#333')
      .text(d => d);

    // Draw horizontal grid lines
    data.solutions.forEach(solution => {
      matrixGroup.append('line')
        .attr('x1', 0)
        .attr('y1', yScale(solution)! + yScale.bandwidth() / 2)
        .attr('x2', matrixWidth)
        .attr('y2', yScale(solution)! + yScale.bandwidth() / 2)
        .attr('stroke', '#f0f0f0')
        .attr('stroke-width', 1)
        .style('pointer-events', 'none');
    });

    // Add x-axis (degree labels)
    const xAxisGroup = g.append('g')
      .attr('class', 'x-axis')
      .attr('transform', `translate(0,${matrixHeight + 10})`);

    xAxisGroup.selectAll('text')
      .data(sortedIntersections)
      .join('text')
      .attr('x', (d, i) => xScale(i.toString())! + xScale.bandwidth() / 2)
      .attr('y', 0)
      .attr('text-anchor', 'start')
      .attr('font-size', '10px')
      .attr('fill', '#666')
      .attr('transform', (d, i) => `rotate(45, ${xScale(i.toString())! + xScale.bandwidth() / 2}, 0)`)
      .text((d, i) => `Set ${i + 1}`);

    // Apply initial transform
    svg.call(zoom.transform as any, transform);

    // Cleanup
    return () => {
      tooltip.remove();
    };
  }, [data, width, height, sortBy, selectedIntersection, onIntersectionClick, transform]);

  const handleZoomIn = () => {
    if (svgRef.current) {
      d3.select(svgRef.current)
        .transition()
        .duration(300)
        .call(d3.zoom<SVGSVGElement, unknown>().scaleBy as any, 1.3);
    }
  };

  const handleZoomOut = () => {
    if (svgRef.current) {
      d3.select(svgRef.current)
        .transition()
        .duration(300)
        .call(d3.zoom<SVGSVGElement, unknown>().scaleBy as any, 0.7);
    }
  };

  const handleReset = () => {
    if (svgRef.current) {
      d3.select(svgRef.current)
        .transition()
        .duration(500)
        .call(d3.zoom<SVGSVGElement, unknown>().transform as any, d3.zoomIdentity);
    }
  };

  if (!data.solutions.length) {
    return (
      <div style={{ width, height, display: 'flex', alignItems: 'center', justifyContent: 'center', border: '1px solid #ccc' }}>
        <div style={{ textAlign: 'center', color: '#666' }}>
          <div style={{ fontSize: '16px', fontWeight: 'bold' }}>No intersection data available</div>
          <div style={{ fontSize: '12px', marginTop: '8px' }}>The UpSet plot will appear once data is loaded</div>
        </div>
      </div>
    );
  }

  return (
    <div style={{ position: 'relative', width, height }}>
      <div style={{ position: 'absolute', top: 10, right: 10, zIndex: 10, display: 'flex', gap: '4px' }}>
        <Button 
          size="small" 
          onClick={() => setSortBy('size')} 
          appearance={sortBy === 'size' ? 'primary' : 'secondary'}
        >
          Sort by Size
        </Button>
        <Button 
          size="small" 
          onClick={() => setSortBy('degree')} 
          appearance={sortBy === 'degree' ? 'primary' : 'secondary'}
        >
          Sort by Degree
        </Button>
        <Button size="small" icon={<ZoomIn24Regular />} onClick={handleZoomIn} title="Zoom In" />
        <Button size="small" icon={<ZoomOut24Regular />} onClick={handleZoomOut} title="Zoom Out" />
        <Button size="small" icon={<ArrowReset24Regular />} onClick={handleReset} title="Reset View" />
      </div>
      <svg
        ref={svgRef}
        width={width}
        height={height}
        style={{ border: '1px solid #ccc' }}
      />
      <div style={{ position: 'absolute', bottom: 10, left: 10, fontSize: '12px', background: 'rgba(255,255,255,0.9)', padding: '8px', borderRadius: '4px' }}>
        <div><strong>UpSet Plot:</strong></div>
        <div style={{ marginTop: '4px', fontSize: '10px', color: '#666' }}>
          Connected dots = solution participation<br/>
          Bar height = intersection size<br/>
          Click bars to select • {data.intersections.length} intersections
        </div>
      </div>
    </div>
  );
};
