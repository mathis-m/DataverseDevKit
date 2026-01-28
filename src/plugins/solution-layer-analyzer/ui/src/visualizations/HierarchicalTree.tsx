import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { HierarchyData, getSeverityColor } from '../types/analytics';
import { createTooltip, showTooltip, hideTooltip } from '../utils/d3-helpers';
import { Button } from '@fluentui/react-components';
import { ZoomIn24Regular, ZoomOut24Regular, ArrowReset24Regular } from '@fluentui/react-icons';

interface HierarchicalTreeProps {
  data: HierarchyData;
  width?: number;
  height?: number;
  onNodeClick?: (nodeId: string) => void;
  selectedNodeId?: string;
}

interface TreeNode extends d3.HierarchyPointNode<HierarchyData> {
  _children?: TreeNode[];
}

export const HierarchicalTree: React.FC<HierarchicalTreeProps> = ({
  data,
  width = 1200,
  height = 800,
  onNodeClick,
  selectedNodeId
}) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const [transform, setTransform] = useState<d3.ZoomTransform>(d3.zoomIdentity);

  useEffect(() => {
    if (!svgRef.current || !data) return;

    // Clear previous content
    d3.select(svgRef.current).selectAll('*').remove();

    const svg = d3.select(svgRef.current);
    const g = svg.append('g').attr('class', 'main-group');

    // Create tooltip
    const tooltip = createTooltip('hierarchical-tree-tooltip');

    // Setup zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.1, 10])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
        setTransform(event.transform);
      });

    svg.call(zoom);

    // Create tree layout
    const treeLayout = d3.tree<HierarchyData>()
      .size([height - 100, width - 300])
      .separation((a, b) => (a.parent === b.parent ? 1 : 2) / a.depth);

    // Create hierarchy
    const root = d3.hierarchy(data);
    const treeData = treeLayout(root) as TreeNode;

    // Collapse nodes initially if depth > 2
    root.descendants().forEach((d: any) => {
      if (d.depth > 2 && d.children) {
        d._children = d.children;
        d.children = null;
      }
    });

    // Center the root
    const centerX = width / 2 - 100;
    const centerY = 50;

    function update(source: TreeNode) {
      const nodes = treeData.descendants();
      const links = treeData.links();

      // Compute new tree layout
      treeLayout(treeData);

      // Update nodes
      const node = g.selectAll<SVGGElement, TreeNode>('g.node')
        .data(nodes, (d: any) => d.data.name);

      // Enter new nodes
      const nodeEnter = node.enter()
        .append('g')
        .attr('class', 'node')
        .attr('transform', `translate(${centerY},${centerX})`)
        .style('cursor', 'pointer')
        .on('click', (event, d) => {
          event.stopPropagation();
          toggle(d);
          update(d);
          if (onNodeClick) {
            onNodeClick(d.data.name);
          }
        });

      // Add circles for nodes
      nodeEnter.append('circle')
        .attr('r', 6)
        .style('fill', d => {
          if (d.data.metadata?.violations?.length > 0) {
            return '#D32F2F'; // Red for violations
          }
          if (d.data.type === 'solution') return '#0078D4';
          if (d.data.type === 'layer') return '#00B294';
          return '#FFB900';
        })
        .style('stroke', d => d.data.name === selectedNodeId ? '#000' : '#fff')
        .style('stroke-width', d => d.data.name === selectedNodeId ? 3 : 2);

      // Add violation markers
      nodeEnter.append('circle')
        .attr('class', 'violation-marker')
        .attr('r', 3)
        .attr('cx', 8)
        .attr('cy', -8)
        .style('fill', '#D32F2F')
        .style('stroke', '#fff')
        .style('stroke-width', 1)
        .style('opacity', d => d.data.metadata?.violations?.length > 0 ? 1 : 0);

      // Add labels
      nodeEnter.append('text')
        .attr('dy', '.35em')
        .attr('x', d => d.children || d._children ? -13 : 13)
        .attr('text-anchor', d => d.children || d._children ? 'end' : 'start')
        .text(d => d.data.name)
        .style('font-size', '12px')
        .style('font-weight', d => d.data.type === 'solution' ? 'bold' : 'normal')
        .style('fill', '#333')
        .clone(true).lower()
        .style('stroke', 'white')
        .style('stroke-width', 3);

      // Add collapse/expand indicator
      nodeEnter.append('text')
        .attr('class', 'toggle-indicator')
        .attr('y', 0)
        .attr('x', 0)
        .attr('text-anchor', 'middle')
        .attr('dy', '.35em')
        .style('font-size', '10px')
        .style('font-weight', 'bold')
        .style('fill', 'white')
        .style('pointer-events', 'none')
        .text(d => {
          if (d._children) return '+';
          if (d.children) return '−';
          return '';
        });

      // Tooltips
      nodeEnter.on('mouseover', function(event, d) {
        const violations = d.data.metadata?.violations || [];
        const content = `
          <strong>${d.data.name}</strong><br/>
          Type: ${d.data.type}<br/>
          ${d.data.value ? `Value: ${d.data.value}<br/>` : ''}
          Depth: ${d.depth}<br/>
          ${violations.length > 0 ? `<span style="color: #ff6b6b">⚠ ${violations.length} violation(s)</span><br/>` : ''}
          ${d.children ? `${d.children.length} children` : ''}
          ${d._children ? `${d._children.length} children (collapsed)` : ''}
        `;
        showTooltip(tooltip, content, event);
      })
      .on('mouseout', () => hideTooltip(tooltip));

      // Update existing nodes
      const nodeUpdate = nodeEnter.merge(node);
      nodeUpdate.transition()
        .duration(500)
        .attr('transform', d => `translate(${d.y + centerY},${d.x + centerX})`);

      nodeUpdate.select('circle')
        .style('stroke', d => d.data.name === selectedNodeId ? '#000' : '#fff')
        .style('stroke-width', d => d.data.name === selectedNodeId ? 3 : 2);

      nodeUpdate.select('.toggle-indicator')
        .text(d => {
          if (d._children) return '+';
          if (d.children) return '−';
          return '';
        });

      // Remove exiting nodes
      node.exit().transition()
        .duration(500)
        .attr('transform', `translate(${source.y + centerY},${source.x + centerX})`)
        .remove();

      // Update links
      const link = g.selectAll<SVGPathElement, d3.HierarchyPointLink<HierarchyData>>('path.link')
        .data(links, (d: any) => d.target.data.name);

      // Enter new links
      const linkEnter = link.enter()
        .insert('path', 'g')
        .attr('class', 'link')
        .attr('d', d3.linkHorizontal<any, TreeNode>()
          .x(() => centerY)
          .y(() => centerX)
        )
        .style('fill', 'none')
        .style('stroke', '#ccc')
        .style('stroke-width', 2);

      // Update existing links
      linkEnter.merge(link).transition()
        .duration(500)
        .attr('d', d3.linkHorizontal<any, TreeNode>()
          .x(d => d.y + centerY)
          .y(d => d.x + centerX)
        );

      // Remove exiting links
      link.exit().transition()
        .duration(500)
        .attr('d', d3.linkHorizontal<any, TreeNode>()
          .x(() => source.y + centerY)
          .y(() => source.x + centerX)
        )
        .remove();

      // Highlight path to selected node
      if (selectedNodeId) {
        const selectedNode = nodes.find(n => n.data.name === selectedNodeId);
        if (selectedNode) {
          const path = selectedNode.ancestors();
          g.selectAll('path.link')
            .style('stroke', (d: any) => {
              return path.includes(d.target) ? '#0078D4' : '#ccc';
            })
            .style('stroke-width', (d: any) => {
              return path.includes(d.target) ? 3 : 2;
            });
        }
      }
    }

    function toggle(d: TreeNode) {
      if (d.children) {
        d._children = d.children;
        d.children = null;
      } else if (d._children) {
        d.children = d._children;
        d._children = null;
      }
    }

    // Initial update
    update(treeData);

    // Apply initial transform
    svg.call(zoom.transform as any, transform);

    // Cleanup
    return () => {
      tooltip.remove();
    };
  }, [data, width, height, selectedNodeId, onNodeClick, transform]);

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

  if (!data) {
    return (
      <div style={{ width, height, display: 'flex', alignItems: 'center', justifyContent: 'center', border: '1px solid #ccc' }}>
        <div style={{ textAlign: 'center', color: '#666' }}>
          <div style={{ fontSize: '16px', fontWeight: 'bold' }}>No hierarchy data available</div>
          <div style={{ fontSize: '12px', marginTop: '8px' }}>The tree will appear once data is loaded</div>
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
        style={{ border: '1px solid #ccc', cursor: 'grab' }}
      />
      <div style={{ position: 'absolute', bottom: 10, left: 10, fontSize: '12px', background: 'rgba(255,255,255,0.9)', padding: '8px', borderRadius: '4px' }}>
        <div><strong>Legend:</strong></div>
        <div style={{ display: 'flex', gap: '10px', marginTop: '4px' }}>
          <div><span style={{ display: 'inline-block', width: '12px', height: '12px', backgroundColor: '#0078D4', borderRadius: '50%', marginRight: '4px' }}></span>Solution</div>
          <div><span style={{ display: 'inline-block', width: '12px', height: '12px', backgroundColor: '#00B294', borderRadius: '50%', marginRight: '4px' }}></span>Layer</div>
          <div><span style={{ display: 'inline-block', width: '12px', height: '12px', backgroundColor: '#FFB900', borderRadius: '50%', marginRight: '4px' }}></span>Component</div>
          <div><span style={{ display: 'inline-block', width: '12px', height: '12px', backgroundColor: '#D32F2F', borderRadius: '50%', marginRight: '4px' }}></span>Violation</div>
        </div>
        <div style={{ marginTop: '4px', fontSize: '10px', color: '#666' }}>
          Click nodes to expand/collapse • Scroll to zoom • Click + hold to drag
        </div>
      </div>
    </div>
  );
};
