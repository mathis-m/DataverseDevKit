import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { ComponentResult } from '../types';
import { makeStyles, tokens, Button, Text } from '@fluentui/react-components';
import { ZoomIn20Regular, ZoomOut20Regular, ArrowReset20Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    position: 'relative',
    width: '100%',
    height: '100%',
  },
  controls: {
    position: 'absolute',
    top: tokens.spacingVerticalM,
    right: tokens.spacingHorizontalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    zIndex: 10,
  },
  svg: {
    width: '100%',
    height: '100%',
    cursor: 'grab',
    ':active': {
      cursor: 'grabbing',
    },
  },
});

interface Node {
  id: string;
  group: 'solution' | 'componentType';
  size: number;
}

interface Link {
  source: string;
  target: string;
  value: number;
}

interface LayerNetworkGraphProps {
  components: ComponentResult[];
  width?: number;
  height?: number;
}

export const LayerNetworkGraph: React.FC<LayerNetworkGraphProps> = ({
  components,
  width = 1200,
  height = 800,
}) => {
  const styles = useStyles();
  const svgRef = useRef<SVGSVGElement>(null);
  const [zoom, setZoom] = useState<d3.ZoomBehavior<SVGSVGElement, unknown> | null>(null);

  useEffect(() => {
    if (!svgRef.current || components.length === 0) return;

    // Clear previous content
    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    // Prepare data: nodes are solutions and component types, links connect them
    const solutionNodes = new Map<string, Node>();
    const typeNodes = new Map<string, Node>();
    const links: Link[] = [];

    components.forEach(component => {
      const typeId = `type_${component.componentType}`;
      if (!typeNodes.has(typeId)) {
        typeNodes.set(typeId, {
          id: typeId,
          group: 'componentType',
          size: 0,
        });
      }
      typeNodes.get(typeId)!.size++;

      component.layerSequence.forEach(solution => {
        const solutionId = `sol_${solution}`;
        if (!solutionNodes.has(solutionId)) {
          solutionNodes.set(solutionId, {
            id: solutionId,
            group: 'solution',
            size: 0,
          });
        }
        solutionNodes.get(solutionId)!.size++;

        // Create link between component type and solution
        const existingLink = links.find(l => l.source === typeId && l.target === solutionId);
        if (existingLink) {
          existingLink.value++;
        } else {
          links.push({ source: typeId, target: solutionId, value: 1 });
        }
      });
    });

    const nodes: Node[] = [...Array.from(typeNodes.values()), ...Array.from(solutionNodes.values())];

    // Set up force simulation
    const simulation = d3.forceSimulation(nodes as any)
      .force('link', d3.forceLink(links).id((d: any) => d.id).distance(150))
      .force('charge', d3.forceManyBody().strength(-300))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide().radius((d: any) => Math.sqrt(d.size) * 5 + 10));

    // Create zoom behavior
    const zoomBehavior = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.1, 10])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
      });

    svg.call(zoomBehavior);
    setZoom(zoomBehavior as any);

    const g = svg.append('g');

    // Draw links
    const link = g.append('g')
      .selectAll('line')
      .data(links)
      .join('line')
      .attr('stroke', tokens.colorNeutralStroke1)
      .attr('stroke-opacity', 0.6)
      .attr('stroke-width', (d: Link) => Math.sqrt(d.value) * 2);

    // Draw nodes
    const node = g.append('g')
      .selectAll('g')
      .data(nodes)
      .join('g')
      .call(d3.drag<SVGGElement, Node>()
        .on('start', dragstarted)
        .on('drag', dragged)
        .on('end', dragended) as any);

    node.append('circle')
      .attr('r', (d: Node) => Math.sqrt(d.size) * 5 + 5)
      .attr('fill', (d: Node) => d.group === 'solution' ? tokens.colorBrandBackground : tokens.colorPaletteGreenBackground2)
      .attr('stroke', '#fff')
      .attr('stroke-width', 2)
      .style('cursor', 'pointer');

    node.append('text')
      .text((d: Node) => d.id.replace('type_', '').replace('sol_', ''))
      .attr('x', 0)
      .attr('y', (d: Node) => Math.sqrt(d.size) * 5 + 20)
      .attr('text-anchor', 'middle')
      .style('font-size', '12px')
      .style('fill', tokens.colorNeutralForeground1)
      .style('pointer-events', 'none');

    node.append('title')
      .text((d: Node) => `${d.id.replace('type_', '').replace('sol_', '')}: ${d.size} components`);

    // Update positions on tick
    simulation.on('tick', () => {
      link
        .attr('x1', (d: any) => d.source.x)
        .attr('y1', (d: any) => d.source.y)
        .attr('x2', (d: any) => d.target.x)
        .attr('y2', (d: any) => d.target.y);

      node.attr('transform', (d: any) => `translate(${d.x},${d.y})`);
    });

    function dragstarted(event: any) {
      if (!event.active) simulation.alphaTarget(0.3).restart();
      event.subject.fx = event.subject.x;
      event.subject.fy = event.subject.y;
    }

    function dragged(event: any) {
      event.subject.fx = event.x;
      event.subject.fy = event.y;
    }

    function dragended(event: any) {
      if (!event.active) simulation.alphaTarget(0);
      event.subject.fx = null;
      event.subject.fy = null;
    }

    return () => {
      simulation.stop();
    };
  }, [components, width, height]);

  const handleZoomIn = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.scaleBy as any, 1.3);
    }
  };

  const handleZoomOut = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.scaleBy as any, 0.7);
    }
  };

  const handleReset = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.transform as any, d3.zoomIdentity);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.controls}>
        <Button icon={<ZoomIn20Regular />} onClick={handleZoomIn} title="Zoom In" />
        <Button icon={<ZoomOut20Regular />} onClick={handleZoomOut} title="Zoom Out" />
        <Button icon={<ArrowReset20Regular />} onClick={handleReset} title="Reset View" />
      </div>
      <svg ref={svgRef} className={styles.svg} width={width} height={height} />
    </div>
  );
};
