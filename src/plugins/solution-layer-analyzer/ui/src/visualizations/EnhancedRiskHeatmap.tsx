import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { SolutionOverlapMatrix, DetailedOverlap, getSeverityColor } from '../types/analytics';
import { createTooltip, showTooltip, hideTooltip } from '../utils/d3-helpers';
import { Button } from '@fluentui/react-components';
import { ZoomIn24Regular, ZoomOut24Regular, ArrowReset24Regular } from '@fluentui/react-icons';

interface EnhancedRiskHeatmapProps {
  data: SolutionOverlapMatrix;
  width?: number;
  height?: number;
  onCellClick?: (solution1: string, solution2: string) => void;
}

export const EnhancedRiskHeatmap: React.FC<EnhancedRiskHeatmapProps> = ({
  data,
  width = 1000,
  height = 800,
  onCellClick
}) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const [transform, setTransform] = useState<d3.ZoomTransform>(d3.zoomIdentity);
  const [hoveredCell, setHoveredCell] = useState<{ row: string; col: string } | null>(null);

  useEffect(() => {
    if (!svgRef.current || !Object.keys(data.matrix).length) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    const svg = d3.select(svgRef.current);
    const g = svg.append('g').attr('class', 'main-group');

    // Create tooltip
    const tooltip = createTooltip('heatmap-tooltip');

    // Setup zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 5])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
        setTransform(event.transform);
      });

    svg.call(zoom);

    // Get solutions
    const solutions = Object.keys(data.matrix);
    const margin = { top: 150, right: 50, bottom: 50, left: 150 };
    const cellSize = Math.min(
      (width - margin.left - margin.right) / solutions.length,
      (height - margin.top - margin.bottom) / solutions.length,
      60
    );

    // Position the main group
    g.attr('transform', `translate(${margin.left},${margin.top})`);

    // Calculate max overlap for color scale
    const maxOverlap = d3.max(
      solutions.flatMap(s1 => 
        solutions.map(s2 => data.matrix[s1]?.[s2] || 0)
      )
    ) || 1;

    // Color scale
    const colorScale = d3.scaleSequential(d3.interpolateYlOrRd)
      .domain([0, maxOverlap]);

    // Get detailed overlap info
    const getDetailedOverlap = (sol1: string, sol2: string): DetailedOverlap | undefined => {
      return data.detailedOverlaps.find(
        d => (d.solution1 === sol1 && d.solution2 === sol2) ||
             (d.solution1 === sol2 && d.solution2 === sol1)
      );
    };

    // Draw cells
    solutions.forEach((rowSolution, rowIndex) => {
      solutions.forEach((colSolution, colIndex) => {
        const value = data.matrix[rowSolution]?.[colSolution] || 0;
        const detailedOverlap = getDetailedOverlap(rowSolution, colSolution);
        
        // Skip diagonal or zero values
        if (rowSolution === colSolution) {
          g.append('rect')
            .attr('x', colIndex * cellSize)
            .attr('y', rowIndex * cellSize)
            .attr('width', cellSize)
            .attr('height', cellSize)
            .attr('fill', '#f5f5f5')
            .attr('stroke', '#ddd')
            .attr('stroke-width', 1);
          return;
        }

        const cell = g.append('g')
          .attr('class', 'cell')
          .attr('transform', `translate(${colIndex * cellSize},${rowIndex * cellSize})`);

        // Cell background
        cell.append('rect')
          .attr('width', cellSize)
          .attr('height', cellSize)
          .attr('fill', value > 0 ? colorScale(value) : '#fff')
          .attr('stroke', hoveredCell?.row === rowSolution || hoveredCell?.col === colSolution ? '#000' : '#ddd')
          .attr('stroke-width', hoveredCell?.row === rowSolution || hoveredCell?.col === colSolution ? 3 : 1)
          .style('cursor', value > 0 ? 'pointer' : 'default')
          .on('mouseover', function(event) {
            if (value > 0) {
              setHoveredCell({ row: rowSolution, col: colSolution });
              d3.select(this)
                .attr('stroke', '#000')
                .attr('stroke-width', 3);

              const content = `
                <strong>${rowSolution} ↔ ${colSolution}</strong><br/>
                Total Overlap: ${detailedOverlap?.totalOverlap || value}<br/>
                ${detailedOverlap?.managedOverlap ? `Managed: ${detailedOverlap.managedOverlap}<br/>` : ''}
                ${detailedOverlap?.unmanagedOverlap ? `Unmanaged: ${detailedOverlap.unmanagedOverlap}<br/>` : ''}
                Severity: <span style="color: ${getSeverityColor(detailedOverlap?.severity || 'low')}">${detailedOverlap?.severity || 'N/A'}</span><br/>
                <br/>
                ${detailedOverlap?.componentTypeBreakdown ? '<strong>Component Types:</strong><br/>' : ''}
                ${detailedOverlap?.componentTypeBreakdown ? 
                  Object.entries(detailedOverlap.componentTypeBreakdown)
                    .map(([type, count]) => `${type}: ${count}`)
                    .join('<br/>') 
                  : ''}
              `;
              showTooltip(tooltip, content, event);
            }
          })
          .on('mouseout', function() {
            if (value > 0) {
              setHoveredCell(null);
              d3.select(this)
                .attr('stroke', '#ddd')
                .attr('stroke-width', 1);
              hideTooltip(tooltip);
            }
          })
          .on('click', function(event) {
            if (value > 0) {
              event.stopPropagation();
              if (onCellClick) {
                onCellClick(rowSolution, colSolution);
              }
            }
          });

        // Severity border indicator
        if (detailedOverlap?.severity) {
          const borderWidth = 3;
          const severityColor = getSeverityColor(detailedOverlap.severity);
          
          // Top border
          cell.append('line')
            .attr('x1', 0)
            .attr('y1', 0)
            .attr('x2', cellSize)
            .attr('y2', 0)
            .attr('stroke', severityColor)
            .attr('stroke-width', borderWidth);
          
          // Right border
          cell.append('line')
            .attr('x1', cellSize)
            .attr('y1', 0)
            .attr('x2', cellSize)
            .attr('y2', cellSize)
            .attr('stroke', severityColor)
            .attr('stroke-width', borderWidth);
        }

        // Cell text
        if (value > 0) {
          cell.append('text')
            .attr('x', cellSize / 2)
            .attr('y', cellSize / 2)
            .attr('dy', '.35em')
            .attr('text-anchor', 'middle')
            .attr('font-size', '10px')
            .attr('font-weight', 'bold')
            .attr('fill', value > maxOverlap / 2 ? '#fff' : '#333')
            .style('pointer-events', 'none')
            .text(value);
        }
      });
    });

    // Row labels
    const rowLabels = g.append('g')
      .attr('class', 'row-labels')
      .attr('transform', `translate(-10,0)`);

    rowLabels.selectAll('text')
      .data(solutions)
      .join('text')
      .attr('x', 0)
      .attr('y', (d, i) => i * cellSize + cellSize / 2)
      .attr('dy', '.35em')
      .attr('text-anchor', 'end')
      .attr('font-size', '11px')
      .attr('font-weight', d => hoveredCell?.row === d ? 'bold' : 'normal')
      .attr('fill', d => hoveredCell?.row === d ? '#0078D4' : '#333')
      .text(d => d);

    // Column labels
    const colLabels = g.append('g')
      .attr('class', 'col-labels')
      .attr('transform', `translate(0,-10)`);

    colLabels.selectAll('text')
      .data(solutions)
      .join('text')
      .attr('x', (d, i) => i * cellSize + cellSize / 2)
      .attr('y', 0)
      .attr('text-anchor', 'start')
      .attr('font-size', '11px')
      .attr('font-weight', d => hoveredCell?.col === d ? 'bold' : 'normal')
      .attr('fill', d => hoveredCell?.col === d ? '#0078D4' : '#333')
      .attr('transform', (d, i) => `rotate(-45, ${i * cellSize + cellSize / 2}, 0)`)
      .text(d => d);

    // Apply initial transform
    svg.call(zoom.transform as any, transform);

    // Cleanup
    return () => {
      tooltip.remove();
    };
  }, [data, width, height, hoveredCell, onCellClick, transform]);

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

  if (!Object.keys(data.matrix).length) {
    return (
      <div style={{ width, height, display: 'flex', alignItems: 'center', justifyContent: 'center', border: '1px solid #ccc' }}>
        <div style={{ textAlign: 'center', color: '#666' }}>
          <div style={{ fontSize: '16px', fontWeight: 'bold' }}>No overlap data available</div>
          <div style={{ fontSize: '12px', marginTop: '8px' }}>The heatmap will appear once data is loaded</div>
        </div>
      </div>
    );
  }

  return (
    <div style={{ position: 'relative', width, height }}>
      <div style={{ position: 'absolute', top: 10, right: 10, zIndex: 10, display: 'flex', gap: '4px' }}>
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
      <div style={{ position: 'absolute', bottom: 10, left: 10, fontSize: '12px', background: 'rgba(255,255,255,0.9)', padding: '8px', borderRadius: '4px', maxWidth: '400px' }}>
        <div><strong>Risk Heatmap:</strong></div>
        <div style={{ display: 'flex', gap: '15px', marginTop: '4px', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
            <div style={{ width: '40px', height: '12px', background: 'linear-gradient(to right, #fff, #ffeda0, #feb24c, #f03b20)' }}></div>
            <span style={{ fontSize: '10px' }}>Overlap Count</span>
          </div>
        </div>
        <div style={{ marginTop: '4px', fontSize: '10px', color: '#666' }}>
          <strong>Border Colors:</strong> 
          <span style={{ color: getSeverityColor('low') }}>■</span> Low
          <span style={{ color: getSeverityColor('medium'), marginLeft: '4px' }}>■</span> Medium
          <span style={{ color: getSeverityColor('high'), marginLeft: '4px' }}>■</span> High
          <span style={{ color: getSeverityColor('critical'), marginLeft: '4px' }}>■</span> Critical
        </div>
        <div style={{ marginTop: '4px', fontSize: '10px', color: '#666' }}>
          Hover cells for details • Click to drill down • {Object.keys(data.matrix).length} solutions
        </div>
      </div>
    </div>
  );
};
