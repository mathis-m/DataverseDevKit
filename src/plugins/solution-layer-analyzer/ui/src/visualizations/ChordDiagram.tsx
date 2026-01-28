import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { ChordDiagramData, ChordDetail, getSeverityColor } from '../types/analytics';
import { createTooltip, showTooltip, hideTooltip } from '../utils/d3-helpers';
import { Button } from '@fluentui/react-components';
import { ZoomIn24Regular, ZoomOut24Regular, ArrowReset24Regular } from '@fluentui/react-icons';

interface ChordDiagramProps {
  data: ChordDiagramData;
  width?: number;
  height?: number;
  onChordClick?: (source: string, target: string) => void;
}

export const ChordDiagram: React.FC<ChordDiagramProps> = ({
  data,
  width = 800,
  height = 800,
  onChordClick
}) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const [transform, setTransform] = useState<d3.ZoomTransform>(d3.zoomIdentity);
  const [hoveredChord, setHoveredChord] = useState<{ source: number; target: number } | null>(null);

  useEffect(() => {
    if (!svgRef.current || !data.solutions.length) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    const svg = d3.select(svgRef.current);
    const g = svg.append('g').attr('class', 'main-group');

    // Create tooltip
    const tooltip = createTooltip('chord-diagram-tooltip');

    // Setup zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 5])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
        setTransform(event.transform);
      });

    svg.call(zoom);

    // Calculate dimensions
    const outerRadius = Math.min(width, height) * 0.4;
    const innerRadius = outerRadius - 30;
    const centerX = width / 2;
    const centerY = height / 2;

    // Center the diagram
    g.attr('transform', `translate(${centerX},${centerY})`);

    // Create chord layout
    const chord = d3.chord()
      .padAngle(0.05)
      .sortSubgroups(d3.descending);

    const arc = d3.arc()
      .innerRadius(innerRadius)
      .outerRadius(outerRadius);

    const ribbon = d3.ribbon()
      .radius(innerRadius);

    const chords = chord(data.matrix);

    // Color scale
    const colorScale = d3.scaleOrdinal<string>()
      .domain(data.solutions)
      .range(d3.schemeCategory10);

    // Calculate severity for each chord
    const getSeverityForChord = (source: number, target: number): string => {
      const detail = data.chordDetails.find(
        cd => (cd.source === source && cd.target === target) ||
              (cd.source === target && cd.target === source)
      );
      if (!detail) return 'low';
      const value = detail.value;
      if (value > 50) return 'critical';
      if (value > 30) return 'high';
      if (value > 10) return 'medium';
      return 'low';
    };

    // Draw ribbons (connections)
    const ribbonGroup = g.append('g')
      .attr('class', 'ribbons')
      .selectAll('path')
      .data(chords)
      .join('path')
      .attr('d', ribbon as any)
      .style('fill', d => {
        const severity = getSeverityForChord(d.source.index, d.target.index);
        return getSeverityColor(severity as any);
      })
      .style('opacity', d => {
        if (!hoveredChord) return 0.6;
        return (hoveredChord.source === d.source.index && hoveredChord.target === d.target.index) ||
               (hoveredChord.source === d.target.index && hoveredChord.target === d.source.index)
          ? 0.9 : 0.1;
      })
      .style('stroke', d => {
        const severity = getSeverityForChord(d.source.index, d.target.index);
        return getSeverityColor(severity as any);
      })
      .style('stroke-width', 1)
      .style('cursor', 'pointer')
      .on('mouseover', function(event, d) {
        setHoveredChord({ source: d.source.index, target: d.target.index });
        
        const detail = data.chordDetails.find(
          cd => (cd.source === d.source.index && cd.target === d.target.index) ||
                (cd.source === d.target.index && cd.target === d.source.index)
        );

        const componentList = detail?.componentNames.slice(0, 10).join('<br/>') || '';
        const remaining = (detail?.componentNames.length || 0) - 10;
        
        const content = `
          <strong>${data.solutions[d.source.index]} ↔ ${data.solutions[d.target.index]}</strong><br/>
          Overlap Count: ${d.source.value}<br/>
          Severity: ${getSeverityForChord(d.source.index, d.target.index)}<br/>
          <br/>
          <strong>Components:</strong><br/>
          ${componentList}
          ${remaining > 0 ? `<br/>... and ${remaining} more` : ''}
        `;
        showTooltip(tooltip, content, event);
      })
      .on('mouseout', () => {
        setHoveredChord(null);
        hideTooltip(tooltip);
      })
      .on('click', (event, d) => {
        event.stopPropagation();
        if (onChordClick) {
          onChordClick(data.solutions[d.source.index], data.solutions[d.target.index]);
        }
      });

    // Draw arcs (solutions)
    const arcGroup = g.append('g')
      .attr('class', 'arcs')
      .selectAll('g')
      .data(chords.groups)
      .join('g');

    arcGroup.append('path')
      .attr('d', arc as any)
      .style('fill', d => colorScale(data.solutions[d.index]))
      .style('stroke', '#fff')
      .style('stroke-width', 2)
      .style('opacity', d => {
        if (!hoveredChord) return 1;
        return hoveredChord.source === d.index || hoveredChord.target === d.index ? 1 : 0.3;
      })
      .style('cursor', 'pointer')
      .on('mouseover', function(event, d) {
        const totalOverlap = d3.sum(data.matrix[d.index]);
        const content = `
          <strong>${data.solutions[d.index]}</strong><br/>
          Total Overlaps: ${totalOverlap}<br/>
          Connections: ${data.matrix[d.index].filter(v => v > 0).length}
        `;
        showTooltip(tooltip, content, event);
      })
      .on('mouseout', () => hideTooltip(tooltip));

    // Add labels
    arcGroup.append('text')
      .each((d: any) => { d.angle = (d.startAngle + d.endAngle) / 2; })
      .attr('dy', '.35em')
      .attr('transform', (d: any) => {
        const angle = d.angle * 180 / Math.PI - 90;
        return `rotate(${angle})translate(${outerRadius + 10})${angle > 90 ? 'rotate(180)' : ''}`;
      })
      .attr('text-anchor', (d: any) => d.angle > Math.PI ? 'end' : 'start')
      .text(d => data.solutions[d.index])
      .style('font-size', '12px')
      .style('font-weight', 'bold')
      .style('pointer-events', 'none')
      .style('fill', '#333');

    // Apply initial transform with centering
    svg.call(zoom.transform as any, transform);

    // Cleanup
    return () => {
      tooltip.remove();
    };
  }, [data, width, height, hoveredChord, onChordClick, transform]);

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
          <div style={{ fontSize: '16px', fontWeight: 'bold' }}>No solution overlaps available</div>
          <div style={{ fontSize: '12px', marginTop: '8px' }}>The chord diagram will appear once data is loaded</div>
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
      <div style={{ position: 'absolute', bottom: 10, left: 10, fontSize: '12px', background: 'rgba(255,255,255,0.9)', padding: '8px', borderRadius: '4px' }}>
        <div><strong>Severity Legend:</strong></div>
        <div style={{ display: 'flex', gap: '10px', marginTop: '4px', flexWrap: 'wrap' }}>
          <div><span style={{ display: 'inline-block', width: '20px', height: '3px', backgroundColor: '#2E7D32', marginRight: '4px' }}></span>Low</div>
          <div><span style={{ display: 'inline-block', width: '20px', height: '3px', backgroundColor: '#F57C00', marginRight: '4px' }}></span>Medium</div>
          <div><span style={{ display: 'inline-block', width: '20px', height: '3px', backgroundColor: '#D32F2F', marginRight: '4px' }}></span>High</div>
          <div><span style={{ display: 'inline-block', width: '20px', height: '3px', backgroundColor: '#B71C1C', marginRight: '4px' }}></span>Critical</div>
        </div>
        <div style={{ marginTop: '4px', fontSize: '10px', color: '#666' }}>
          Hover arcs/ribbons for details • Click ribbons to drill down • {data.solutions.length} solutions
        </div>
      </div>
    </div>
  );
};
