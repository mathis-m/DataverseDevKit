import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { NetworkGraphData, NetworkNode, NetworkLink, getRiskColor } from '../types/analytics';
import { createTooltip, showTooltip, hideTooltip, createDragBehavior, formatNumber } from '../utils/d3-helpers';
import { Button } from '@fluentui/react-components';
import { ZoomIn24Regular, ZoomOut24Regular, ArrowReset24Regular } from '@fluentui/react-icons';

interface ForceDirectedNetworkProps {
  data: NetworkGraphData;
  width?: number;
  height?: number;
  onNodeClick?: (nodeId: string) => void;
  selectedNodes?: string[];
}

interface SimulationNode extends NetworkNode, d3.SimulationNodeDatum {}
interface SimulationLink extends NetworkLink, d3.SimulationLinkDatum<SimulationNode> {}

export const ForceDirectedNetwork: React.FC<ForceDirectedNetworkProps> = ({
  data,
  width = 1000,
  height = 700,
  onNodeClick,
  selectedNodes = []
}) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const [transform, setTransform] = useState<d3.ZoomTransform>(d3.zoomIdentity);

  useEffect(() => {
    if (!svgRef.current || !data.nodes.length) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    const svg = d3.select(svgRef.current);
    const g = svg.append('g').attr('class', 'main-group');

    // Create tooltip
    const tooltip = createTooltip('force-network-tooltip');

    // Setup zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.1, 10])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
        setTransform(event.transform);
      });

    svg.call(zoom);

    // Prepare data
    const nodes: SimulationNode[] = data.nodes.map(d => ({ ...d }));
    const links: SimulationLink[] = data.links.map(d => ({
      ...d,
      source: d.source,
      target: d.target
    }));

    // Color scale
    const colorScale = d3.scaleOrdinal<string>()
      .domain(['managed', 'unmanaged', 'solution', 'component'])
      .range(['#0078D4', '#E74856', '#00B294', '#FFB900']);

    // Create force simulation
    const simulation = d3.forceSimulation<SimulationNode>(nodes)
      .force('link', d3.forceLink<SimulationNode, SimulationLink>(links)
        .id(d => d.id)
        .distance(d => 80 + (d.value * 10))
      )
      .force('charge', d3.forceManyBody().strength(-300))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide().radius(d => (d as SimulationNode).size + 5));

    // Draw links
    const link = g.append('g')
      .attr('class', 'links')
      .selectAll('line')
      .data(links)
      .join('line')
      .attr('stroke', '#999')
      .attr('stroke-opacity', 0.6)
      .attr('stroke-width', d => Math.sqrt(d.value));

    // Draw nodes
    const node = g.append('g')
      .attr('class', 'nodes')
      .selectAll('g')
      .data(nodes)
      .join('g')
      .call(createDragBehavior(simulation) as any);

    // Node circles
    node.append('circle')
      .attr('r', d => d.size)
      .attr('fill', d => {
        if (d.metadata?.riskScore) {
          return getRiskColor(d.metadata.riskScore);
        }
        return colorScale(d.group);
      })
      .attr('stroke', d => selectedNodes.includes(d.id) ? '#000' : '#fff')
      .attr('stroke-width', d => selectedNodes.includes(d.id) ? 3 : 1.5)
      .style('cursor', 'pointer')
      .on('click', (event, d) => {
        event.stopPropagation();
        onNodeClick?.(d.id);
      })
      .on('mouseover', function(event, d) {
        d3.select(this)
          .transition()
          .duration(200)
          .attr('r', d.size * 1.3);

        const content = `
          <strong>${d.label}</strong><br/>
          Type: ${d.type}<br/>
          ${d.metadata?.riskScore ? `Risk Score: ${d.metadata.riskScore}<br/>` : ''}
          ${d.metadata?.layerDepth ? `Layer Depth: ${d.metadata.layerDepth}<br/>` : ''}
          ${d.metadata?.componentsModified ? `Components: ${d.metadata.componentsModified}<br/>` : ''}
          Size: ${d.size}
        `;
        showTooltip(tooltip, content, event);
      })
      .on('mouseout', function(event, d) {
        d3.select(this)
          .transition()
          .duration(200)
          .attr('r', d.size);
        hideTooltip(tooltip);
      });

    // Node labels
    node.append('text')
      .text(d => d.label)
      .attr('x', 0)
      .attr('y', d => d.size + 15)
      .attr('text-anchor', 'middle')
      .attr('font-size', '10px')
      .attr('font-weight', d => d.type === 'solution' ? 'bold' : 'normal')
      .style('pointer-events', 'none')
      .style('user-select', 'none');

    // Update positions on tick
    simulation.on('tick', () => {
      link
        .attr('x1', d => (d.source as SimulationNode).x!)
        .attr('y1', d => (d.source as SimulationNode).y!)
        .attr('x2', d => (d.target as SimulationNode).x!)
        .attr('y2', d => (d.target as SimulationNode).y!);

      node.attr('transform', d => `translate(${d.x},${d.y})`);
    });

    // Apply initial transform
    svg.call(zoom.transform as any, transform);

    // Cleanup
    return () => {
      simulation.stop();
      tooltip.remove();
    };
  }, [data, width, height, selectedNodes, onNodeClick]);

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
        style={{ border: '1px solid #ccc', cursor: 'grab' }}
      />
      <div style={{ position: 'absolute', bottom: 10, left: 10, fontSize: '12px', background: 'rgba(255,255,255,0.9)', padding: '8px', borderRadius: '4px' }}>
        <div><strong>Legend:</strong></div>
        <div style={{ display: 'flex', gap: '10px', marginTop: '4px' }}>
          <div><span style={{ display: 'inline-block', width: '12px', height: '12px', backgroundColor: '#0078D4', marginRight: '4px' }}></span>Managed</div>
          <div><span style={{ display: 'inline-block', width: '12px', height: '12px', backgroundColor: '#E74856', marginRight: '4px' }}></span>Unmanaged</div>
        </div>
        <div style={{ marginTop: '4px', fontSize: '10px', color: '#666' }}>
          Drag nodes • Scroll to zoom • {data.nodes.length} nodes • {data.links.length} connections
        </div>
      </div>
    </div>
  );
};
