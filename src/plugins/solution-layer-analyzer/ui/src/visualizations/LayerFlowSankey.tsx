import React, { useEffect, useRef } from 'react';
import * as d3 from 'd3';
import { ComponentResult } from '../types';

interface LayerFlowSankeyProps {
  components: ComponentResult[];
  width?: number;
  height?: number;
}

export const LayerFlowSankey: React.FC<LayerFlowSankeyProps> = ({
  components,
  width = 800,
  height = 600,
}) => {
  const svgRef = useRef<SVGSVGElement>(null);

  useEffect(() => {
    if (!svgRef.current || components.length === 0) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    // Prepare Sankey data
    const nodesMap = new Map<string, number>();
    const links: { source: number; target: number; value: number }[] = [];

    components.forEach(component => {
      for (let i = 0; i < component.layerSequence.length - 1; i++) {
        const source = component.layerSequence[i];
        const target = component.layerSequence[i + 1];

        if (!nodesMap.has(source)) {
          nodesMap.set(source, nodesMap.size);
        }
        if (!nodesMap.has(target)) {
          nodesMap.set(target, nodesMap.size);
        }

        const sourceIdx = nodesMap.get(source)!;
        const targetIdx = nodesMap.get(target)!;

        const existingLink = links.find(l => l.source === sourceIdx && l.target === targetIdx);
        if (existingLink) {
          existingLink.value++;
        } else {
          links.push({ source: sourceIdx, target: targetIdx, value: 1 });
        }
      }
    });

    const nodes = Array.from(nodesMap.keys()).map(name => ({ name }));

    // Create SVG
    const svg = d3.select(svgRef.current)
      .attr('width', width)
      .attr('height', height);

    const margin = { top: 20, right: 20, bottom: 20, left: 20 };
    const innerWidth = width - margin.left - margin.right;
    const innerHeight = height - margin.top - margin.bottom;

    const g = svg.append('g')
      .attr('transform', `translate(${margin.left},${margin.top})`);

    // Calculate positions (simplified Sankey-like layout)
    const nodeWidth = 15;
    const nodePadding = 20;

    // Group nodes by layer position
    const layerGroups = new Map<number, string[]>();
    components.forEach(component => {
      component.layerSequence.forEach((solution, idx) => {
        if (!layerGroups.has(idx)) {
          layerGroups.set(idx, []);
        }
        if (!layerGroups.get(idx)!.includes(solution)) {
          layerGroups.get(idx)!.push(solution);
        }
      });
    });

    const maxLayers = Math.max(...Array.from(layerGroups.keys())) + 1;
    const layerSpacing = innerWidth / (maxLayers + 1);

    // Assign positions
    nodes.forEach(node => {
      let layer = 0;
      for (const [layerIdx, solutions] of layerGroups.entries()) {
        if (solutions.includes(node.name)) {
          layer = layerIdx;
          break;
        }
      }

      const layerNodes = layerGroups.get(layer) || [];
      const indexInLayer = layerNodes.indexOf(node.name);
      const layerHeight = innerHeight - nodePadding * (layerNodes.length - 1);
      const nodeHeight = layerHeight / layerNodes.length;

      (node as any).x = layerSpacing * (layer + 1);
      (node as any).y = indexInLayer * (nodeHeight + nodePadding) + nodeHeight / 2;
      (node as any).height = nodeHeight;
    });

    // Draw links
    const linkColor = d3.scaleOrdinal(d3.schemeCategory10);
    
    links.forEach((link, i) => {
      const source = nodes[link.source] as any;
      const target = nodes[link.target] as any;
      const linkWidth = Math.max(2, Math.min(20, link.value * 2));

      g.append('path')
        .attr('d', () => {
          const x0 = source.x + nodeWidth;
          const x1 = target.x;
          const xi = d3.interpolateNumber(x0, x1);
          const x2 = xi(0.5);
          const y0 = source.y;
          const y1 = target.y;
          return `M${x0},${y0}C${x2},${y0} ${x2},${y1} ${x1},${y1}`;
        })
        .attr('fill', 'none')
        .attr('stroke', linkColor(i.toString()))
        .attr('stroke-width', linkWidth)
        .attr('opacity', 0.4)
        .append('title')
        .text(`${source.name} → ${target.name}: ${link.value} components`);
    });

    // Draw nodes
    const nodeGroup = g.selectAll('.node')
      .data(nodes)
      .enter()
      .append('g')
      .attr('class', 'node')
      .attr('transform', (d: any) => `translate(${d.x},${d.y - d.height / 2})`);

    nodeGroup.append('rect')
      .attr('width', nodeWidth)
      .attr('height', (d: any) => d.height)
      .attr('fill', (_d, i) => d3.schemeCategory10[i % 10])
      .attr('opacity', 0.8)
      .append('title')
      .text((d: any) => d.name);

    nodeGroup.append('text')
      .attr('x', nodeWidth + 6)
      .attr('y', (d: any) => d.height / 2)
      .attr('dy', '0.35em')
      .attr('text-anchor', 'start')
      .style('font-size', '12px')
      .text((d: any) => d.name)
      .style('pointer-events', 'none');

  }, [components, width, height]);

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
        <strong>Layer Flow Diagram:</strong> Shows how components flow from base layers (left) to top layers (right). 
        Thicker flows indicate more components layering between those solutions.
      </div>
    </div>
  );
};
